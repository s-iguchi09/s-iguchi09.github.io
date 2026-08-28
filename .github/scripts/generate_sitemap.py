#!/usr/bin/env python3
"""
Generate sitemap.xml for s-iguchi09.github.io.

This script scans all HTML files in the repository root,
builds a multilingual sitemap with hreflang annotations
for English and Japanese pages, and writes sitemap.xml.

URL structure:
  - Base URL: loaded from _config.yml (url), fallback to DEFAULT_BASE_URL
  - English pages: <base-url>/<path>
  - Japanese pages: <base-url>/ja/<path>
  - index.html  → trailing-slash URL  (e.g. / or /ja/)
"""

import os
import re
import sys
import glob
import html
import datetime
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from typing import Optional
from xml.sax.saxutils import escape as xml_escape

REPO_ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
TODAY = datetime.date.today().isoformat()
DEFAULT_BASE_URL = "https://s-iguchi09.github.io"


def load_base_url() -> str:
    """Load site URL from _config.yml, falling back to DEFAULT_BASE_URL."""
    config_path = os.path.join(REPO_ROOT, "_config.yml")
    try:
        with open(config_path, encoding="utf-8", errors="replace") as fh:
            content = fh.read()
    except OSError:
        return DEFAULT_BASE_URL

    match = re.search(r'^\s*url:\s*["\']?([^"\n\']+)', content, re.MULTILINE)
    if not match:
        return DEFAULT_BASE_URL

    return match.group(1).strip().rstrip("/") or DEFAULT_BASE_URL


def load_config_excludes() -> set:
    """Return the top-level names listed under `exclude:` in _config.yml.

    Jekyll never publishes those paths, so any HTML found under them would be
    written into the sitemap as a URL that answers 404. Search engines report a
    sitemap full of 404 URLs as an error, so the scan has to honour the same
    exclusion list the site build uses.
    """
    config_path = os.path.join(REPO_ROOT, "_config.yml")
    try:
        with open(config_path, encoding="utf-8", errors="replace") as fh:
            lines = fh.read().splitlines()
    except OSError:
        return set()

    names = set()
    in_exclude = False
    for line in lines:
        if re.match(r"^exclude:\s*(#.*)?$", line):
            in_exclude = True
            continue
        if not in_exclude:
            continue
        # A quoted entry may contain spaces ('- "draft pages"'), so the quoted
        # forms are matched whole; only an unquoted value ends at whitespace.
        item = re.match(r"""^\s+-\s*(?:"([^"]*)"|'([^']*)'|([^"'#\s]+))""", line)
        if item:
            value = next(group for group in item.groups() if group is not None)
            if value:
                # Only the leading path segment matters (e.g. "docs/rules" -> "docs")
                names.add(value.strip("/").split("/")[0])
            continue
        # Blank lines and comments are skipped wherever they sit; an indented
        # comment belongs to the block and must not be read as its end.
        if line.strip() == "" or line.strip().startswith("#"):
            continue
        # A non-indented line ends the exclude block
        in_exclude = False
    return names


BASE_URL = load_base_url()

# Directories to exclude from scanning.
# The list from _config.yml (docs, tools, ...) is merged in so that HTML under
# directories the site build drops never reaches the sitemap.
EXCLUDE_DIRS = {"_includes", "_layouts", "_site", ".github", ".git"} | load_config_excludes()

# Pages whose front matter sets `noindex: true` are excluded from the sitemap.
# This mirrors the condition in _includes/head.html, which emits
# <meta name="robots" content="noindex"> for the same pages; listing them here
# would send search engines a contradictory signal.
#
# Until 2026-08-29 this matched a hard-coded path fragment
# ("wpf-standard-control-demo/") instead. That coupled the sitemap to one
# directory and had to be kept in sync with head.html by hand. Reading the front
# matter keeps the two in step on their own.
# Front matter is scanned rather than parsed as YAML: the workflow installs a
# bare Python via actions/setup-python, so PyYAML is not available and pulling it
# in for one boolean is not worth the dependency. The pattern is therefore pinned
# to what head.html actually reads:
#
#   - the key must sit at column 0, so a nested `seo:\n  noindex: true` does not
#     match; Liquid's `page.noindex` only sees top-level keys
#   - the value accepts the YAML 1.1 spellings Jekyll resolves to boolean true
#     (Psych treats yes/on as booleans, and Liquid then compares equal to true)
#   - a trailing `# comment` is allowed, since YAML ignores it
#   - a quoted "true" is a string, not a boolean, so it is left out on purpose
NOINDEX_FRONT_MATTER_RE = re.compile(
    r"^noindex[ \t]*:[ \t]*(?:true|True|TRUE|yes|Yes|YES|on|On|ON)[ \t]*(?:#.*)?$"
)


def is_excluded_from_sitemap(rel_path: str) -> bool:
    """Return True if the page is noindex and must not appear in the sitemap.

    Only a top-level boolean true counts, matching `page.noindex == true` in
    _includes/head.html. A quoted `"true"` or `"false"` is a YAML string, which
    neither side treats as noindex.
    """
    path = os.path.join(REPO_ROOT, rel_path)
    try:
        # utf-8-sig so a byte order mark does not hide the opening delimiter.
        with open(path, encoding="utf-8-sig") as handle:
            # Front matter has to start on the first line, or there is none.
            if handle.readline().strip() != "---":
                return False
            for line in handle:
                if line.strip() == "---":
                    return False
                if NOINDEX_FRONT_MATTER_RE.match(line):
                    return True
    except OSError:
        # An unreadable page is left in the sitemap rather than silently dropped.
        return False
    return False


def is_pair_excluded_from_sitemap(*rel_paths: Optional[str]) -> bool:
    """Return True when any side of an English/Japanese pair is noindex.

    The two languages are emitted together, so `noindex: true` on either one has
    to drop both. Otherwise the sitemap would advertise a page that head.html
    tells search engines to leave out.
    """
    return any(path and is_excluded_from_sitemap(path) for path in rel_paths)

# Priority rules (matched in order, first match wins)
PRIORITY_RULES = [
    # index pages
    (lambda p: p in ("index.html", "ja/index.html"), "1.0", "weekly"),
    # top-level app pages
    (lambda p: p.startswith("apps/") and p.count("/") == 1, "0.9", "weekly"),
    (lambda p: p.startswith("ja/apps/") and p.count("/") == 2, "0.9", "weekly"),
    # article pages
    (lambda p: p.startswith("articles/"), "0.8", "monthly"),
    (lambda p: p.startswith("ja/articles/"), "0.8", "monthly"),
    # about
    (lambda p: os.path.basename(p) == "about.html", "0.8", "monthly"),
    # contact
    (lambda p: os.path.basename(p) == "contact.html", "0.5", "monthly"),
    # privacy
    (lambda p: os.path.basename(p) == "privacy.html", "0.3", "monthly"),
    # app detail pages (default)
    (lambda p: True, "0.6", "monthly"),
]


def get_priority_and_freq(rel_path: str):
    for rule, priority, freq in PRIORITY_RULES:
        if rule(rel_path):
            return priority, freq
    return "0.5", "monthly"


def get_git_lastmod(rel_path: str) -> str:
    """Return the last-modified date (YYYY-MM-DD) of a file from git history.

    Falls back to TODAY if the file has no git history.
    """
    try:
        result = subprocess.run(
            ["git", "log", "--format=%ci", "-1", "--", rel_path],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=True,
        )
        date_str = result.stdout.strip()
        if date_str:
            # git outputs "YYYY-MM-DD HH:MM:SS +HHMM"; take the date portion
            return date_str[:10]
    except subprocess.CalledProcessError:
        pass
    return TODAY


def image_element(url: str) -> str:
    """Build one <image:image> entry, XML-escaping the URL.

    An image URL can legitimately contain '&' (query strings, for example),
    which must not be written into the sitemap verbatim.
    """
    return f"    <image:image><image:loc>{xml_escape(url)}</image:loc></image:image>"


def extract_images_from_file(abs_path: str) -> list:
    """Return a list of absolute image URLs found in an HTML or Markdown file.

    Looks for HTML <img src="..."> tags in every file type, and additionally for
    ![...](src) syntax in Markdown files. Articles wrap figures in
    <figure class="article-figure"><img ...></figure> so that a caption can be
    attached, so Markdown files must be scanned for both notations.
    Only site-relative paths (starting with a single /) are included; a
    protocol-relative "//host/..." reference points at another host, so
    prefixing it with BASE_URL would produce a bogus URL.
    """
    if not os.path.isfile(abs_path):
        return []
    try:
        with open(abs_path, encoding="utf-8", errors="replace") as fh:
            content = fh.read()
    except OSError:
        return []

    def is_site_relative(src: str) -> bool:
        return src.startswith("/") and not src.startswith("//")

    images = []
    if abs_path.endswith(".md"):
        # Markdown image syntax: ![alt](path), including the ![alt](<path> "title") form.
        # Destinations can carry HTML entities too, so decode before matching and
        # let image_element() re-escape for XML on output.
        for src in re.findall(r'!\[[^\]]*\]\(([^)]+)\)', content):
            src = src.strip()
            if not src:  # e.g. "![alt]( )"
                continue
            if src.startswith("<"):
                # Angle-bracketed destination: everything up to the closing '>'
                src = src[1:].split(">", 1)[0]
            else:
                src = src.split()[0]  # strip optional title (e.g. "url title")
            src = html.unescape(src)
            if is_site_relative(src):
                images.append(BASE_URL + src)
    # HTML <img src="..."> (also handles single quotes).
    # The attribute value is HTML-escaped in the source, so "&amp;" must be decoded
    # back to "&" here; image_element() re-escapes it for XML on output.
    for src in re.findall(r'<img\s[^>]*\bsrc=["\']([^"\']+)["\']', content, re.IGNORECASE):
        src = html.unescape(src)
        if is_site_relative(src):
            images.append(BASE_URL + src)
    # Deduplicate while preserving order
    seen = set()
    unique = []
    for url in images:
        if url not in seen:
            seen.add(url)
            unique.append(url)
    return unique


def rel_path_to_url(rel_path: str) -> str:
    """Convert a relative file path to the public URL."""
    # index.html files → directory-style URL
    parts = rel_path.replace("\\", "/").split("/")
    if parts[-1] == "index.html":
        dir_part = "/".join(parts[:-1])
        return f"{BASE_URL}/{dir_part}/" if dir_part else f"{BASE_URL}/"
    return f"{BASE_URL}/{rel_path.replace(chr(92), '/')}"


def collect_english_paths() -> list:
    """Return sorted list of English HTML file paths relative to REPO_ROOT."""
    paths = []
    for html_file in glob.glob(
        os.path.join(REPO_ROOT, "**", "*.html"), recursive=True
    ):
        rel = os.path.relpath(html_file, REPO_ROOT).replace("\\", "/")
        # Skip files inside excluded directories
        top_dir = rel.split("/")[0]
        if top_dir in EXCLUDE_DIRS:
            continue
        # Skip Japanese pages (handled separately via pairing)
        if rel.startswith("ja/"):
            continue
        # Skip noindex pages, on either side of the en/ja pair
        if is_pair_excluded_from_sitemap(rel, find_ja_counterpart(rel)):
            continue
        paths.append(rel)
    return sorted(paths)


def find_ja_counterpart(en_path: str) -> Optional[str]:
    """Return the Japanese counterpart path if it exists, else None."""
    ja_path = "ja/" + en_path
    full = os.path.join(REPO_ROOT, ja_path)
    return ja_path if os.path.exists(full) else None


def collect_article_en_paths() -> list:
    """Return sorted list of English article slugs from _articles_en/*.md."""
    slugs = []
    collection_dir = os.path.join(REPO_ROOT, "_articles_en")
    if os.path.isdir(collection_dir):
        for md_file in glob.glob(os.path.join(collection_dir, "*.md")):
            slug = os.path.splitext(os.path.basename(md_file))[0]
            # Skip noindex articles, on either side of the en/ja pair
            ja_md = (
                f"_articles_ja/{slug}.md"
                if find_ja_article_counterpart(slug)
                else None
            )
            if is_pair_excluded_from_sitemap(f"_articles_en/{slug}.md", ja_md):
                continue
            slugs.append(slug)
    return sorted(slugs)


def find_ja_article_counterpart(slug: str) -> bool:
    """Return True if a Japanese article counterpart exists for the given slug."""
    ja_path = os.path.join(REPO_ROOT, "_articles_ja", slug + ".md")
    return os.path.exists(ja_path)


def build_article_url_entry(slug: str, has_ja: bool) -> str:
    """Build one or two <url> XML blocks for an English (and optional Japanese) article."""
    en_rel = f"articles/{slug}/"
    ja_rel = f"ja/articles/{slug}/"
    en_url = f"{BASE_URL}/{en_rel}"
    ja_url = f"{BASE_URL}/{ja_rel}"
    priority, changefreq = get_priority_and_freq(en_rel)

    en_md_rel = f"_articles_en/{slug}.md"
    en_lastmod = get_git_lastmod(en_md_rel)
    en_images = extract_images_from_file(os.path.join(REPO_ROOT, en_md_rel))

    lines = []

    # English entry
    lines.append("  <url>")
    lines.append(f"    <loc>{en_url}</loc>")
    lines.append(f'    <xhtml:link rel="alternate" hreflang="en" href="{en_url}" />')
    if has_ja:
        lines.append(f'    <xhtml:link rel="alternate" hreflang="ja" href="{ja_url}" />')
    lines.append(f'    <xhtml:link rel="alternate" hreflang="x-default" href="{en_url}" />')
    for img_url in en_images:
        lines.append(image_element(img_url))
    lines.append(f"    <lastmod>{en_lastmod}</lastmod>")
    lines.append(f"    <changefreq>{changefreq}</changefreq>")
    lines.append(f"    <priority>{priority}</priority>")
    lines.append("  </url>")

    if has_ja:
        ja_priority, ja_changefreq = get_priority_and_freq(ja_rel)
        ja_md_rel = f"_articles_ja/{slug}.md"
        ja_lastmod = get_git_lastmod(ja_md_rel)
        ja_images = extract_images_from_file(os.path.join(REPO_ROOT, ja_md_rel))
        lines.append("")
        lines.append("  <url>")
        lines.append(f"    <loc>{ja_url}</loc>")
        lines.append(f'    <xhtml:link rel="alternate" hreflang="en" href="{en_url}" />')
        lines.append(f'    <xhtml:link rel="alternate" hreflang="ja" href="{ja_url}" />')
        lines.append(f'    <xhtml:link rel="alternate" hreflang="x-default" href="{en_url}" />')
        for img_url in ja_images:
            lines.append(image_element(img_url))
        lines.append(f"    <lastmod>{ja_lastmod}</lastmod>")
        lines.append(f"    <changefreq>{ja_changefreq}</changefreq>")
        lines.append(f"    <priority>{ja_priority}</priority>")
        lines.append("  </url>")

    return "\n".join(lines)


def build_url_entry(en_path: str, ja_path: Optional[str]) -> str:
    """Build one or two <url> XML blocks for an English (and optional Japanese) page."""
    en_url = rel_path_to_url(en_path)
    priority, changefreq = get_priority_and_freq(en_path)

    en_lastmod = get_git_lastmod(en_path)
    en_images = extract_images_from_file(os.path.join(REPO_ROOT, en_path))

    lines = []

    # English entry
    lines.append("  <url>")
    lines.append(f"    <loc>{en_url}</loc>")
    lines.append(
        f'    <xhtml:link rel="alternate" hreflang="en" href="{en_url}" />'
    )
    if ja_path:
        ja_url = rel_path_to_url(ja_path)
        lines.append(
            f'    <xhtml:link rel="alternate" hreflang="ja" href="{ja_url}" />'
        )
    # x-default points to the English version
    lines.append(
        f'    <xhtml:link rel="alternate" hreflang="x-default" href="{en_url}" />'
    )
    for img_url in en_images:
        lines.append(image_element(img_url))
    lines.append(f"    <lastmod>{en_lastmod}</lastmod>")
    lines.append(f"    <changefreq>{changefreq}</changefreq>")
    lines.append(f"    <priority>{priority}</priority>")
    lines.append("  </url>")

    if ja_path:
        ja_url = rel_path_to_url(ja_path)
        ja_priority, ja_changefreq = get_priority_and_freq(ja_path)
        ja_lastmod = get_git_lastmod(ja_path)
        ja_images = extract_images_from_file(os.path.join(REPO_ROOT, ja_path))
        lines.append("")
        lines.append("  <url>")
        lines.append(f"    <loc>{ja_url}</loc>")
        lines.append(
            f'    <xhtml:link rel="alternate" hreflang="en" href="{en_url}" />'
        )
        lines.append(
            f'    <xhtml:link rel="alternate" hreflang="ja" href="{ja_url}" />'
        )
        # x-default points to the English version
        lines.append(
            f'    <xhtml:link rel="alternate" hreflang="x-default" href="{en_url}" />'
        )
        for img_url in ja_images:
            lines.append(image_element(img_url))
        lines.append(f"    <lastmod>{ja_lastmod}</lastmod>")
        lines.append(f"    <changefreq>{ja_changefreq}</changefreq>")
        lines.append(f"    <priority>{ja_priority}</priority>")
        lines.append("  </url>")

    return "\n".join(lines)


def generate_sitemap() -> str:
    en_paths = collect_english_paths()
    blocks = []
    for en_path in en_paths:
        ja_path = find_ja_counterpart(en_path)
        blocks.append(build_url_entry(en_path, ja_path))

    for slug in collect_article_en_paths():
        has_ja = find_ja_article_counterpart(slug)
        blocks.append(build_article_url_entry(slug, has_ja))

    body = "\n\n".join(blocks)
    sitemap = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"\n'
        '        xmlns:xhtml="http://www.w3.org/1999/xhtml"\n'
        '        xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">\n\n'
        + body
        + "\n\n</urlset>\n"
    )
    return sitemap


SITEMAP_NS = "http://www.sitemaps.org/schemas/sitemap/0.9"

# Limits defined by the sitemap protocol (sitemaps.org).
MAX_URLS = 50000
MAX_BYTES = 50 * 1024 * 1024

SITEMAP_FILENAME = "sitemap.xml"
SITEMAP_INDEX_FILENAME = "sitemap_index.xml"


def latest_lastmod(sitemap: str) -> str:
    """Return the newest <lastmod> in the sitemap, or TODAY if there is none.

    Deriving the index <lastmod> from the sitemap contents keeps the index
    stable: it only moves when a page actually changed, so a rerun that
    produces the same sitemap produces the same index and creates no diff.
    """
    root = ET.fromstring(sitemap)
    dates = [
        (node.text or "").strip()
        for node in root.iter(f"{{{SITEMAP_NS}}}lastmod")
        if (node.text or "").strip()
    ]
    return max(dates) if dates else TODAY


def generate_sitemap_index(sitemap: str) -> str:
    """Build the sitemap index that points at sitemap.xml.

    Search Console keeps the fetch state of a submitted sitemap per URL, so a
    sitemap stuck on "couldn't fetch" stays stuck even after being removed and
    resubmitted under the same path. The index gives Google a second, distinct
    entry point to the same URL set, which can be submitted independently.
    """
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
        "  <sitemap>\n"
        f"    <loc>{BASE_URL}/{SITEMAP_FILENAME}</loc>\n"
        f"    <lastmod>{latest_lastmod(sitemap)}</lastmod>\n"
        "  </sitemap>\n"
        "</sitemapindex>\n"
    )


def validate_sitemap_index(index: str) -> int:
    """Raise ValueError if the sitemap index would be rejected by a crawler.

    Returns the number of referenced sitemaps on success.
    """
    try:
        root = ET.fromstring(index)
    except ET.ParseError as exc:
        raise ValueError(f"{SITEMAP_INDEX_FILENAME} is not well-formed XML: {exc}") from exc

    if root.tag != f"{{{SITEMAP_NS}}}sitemapindex":
        raise ValueError(f"unexpected root element: {root.tag}")

    entries = root.findall(f"{{{SITEMAP_NS}}}sitemap")
    if not entries:
        raise ValueError(f"{SITEMAP_INDEX_FILENAME} contains no <sitemap> entry")

    for entry in entries:
        loc = entry.find(f"{{{SITEMAP_NS}}}loc")
        if loc is None or not (loc.text or "").strip():
            raise ValueError("a <sitemap> entry has no <loc>")
        # A sitemap may only be referenced from the same site it belongs to.
        if not loc.text.strip().startswith(BASE_URL + "/"):
            raise ValueError(f"<loc> points outside the site: {loc.text.strip()}")

    return len(entries)


def validate_sitemap(sitemap: str) -> int:
    """Raise ValueError if the generated sitemap would be rejected by a crawler.

    Search Console reports a malformed or oversized sitemap as an error and
    keeps the previously submitted one in that state, so a broken sitemap must
    never be written out and pushed. Checks performed here:

      - the document is well-formed XML and the root is the sitemap <urlset>
      - every entry carries a non-empty <loc> under BASE_URL
      - the protocol limits on URL count and uncompressed size are respected

    Returns the number of URLs on success.
    """
    try:
        root = ET.fromstring(sitemap)
    except ET.ParseError as exc:
        raise ValueError(f"sitemap.xml is not well-formed XML: {exc}") from exc

    if root.tag != f"{{{SITEMAP_NS}}}urlset":
        raise ValueError(f"unexpected root element: {root.tag}")

    urls = root.findall(f"{{{SITEMAP_NS}}}url")
    if not urls:
        raise ValueError("sitemap.xml contains no <url> entry")
    if len(urls) > MAX_URLS:
        raise ValueError(f"sitemap.xml has {len(urls)} URLs (limit is {MAX_URLS})")

    size = len(sitemap.encode("utf-8"))
    if size > MAX_BYTES:
        raise ValueError(f"sitemap.xml is {size} bytes (limit is {MAX_BYTES})")

    for url in urls:
        loc = url.find(f"{{{SITEMAP_NS}}}loc")
        if loc is None or not (loc.text or "").strip():
            raise ValueError("a <url> entry has no <loc>")
        if not loc.text.strip().startswith(BASE_URL + "/"):
            raise ValueError(f"<loc> points outside the site: {loc.text.strip()}")

    return len(urls)


def write_output(filename: str, content: str) -> str:
    """Write content to filename, replacing the destination atomically.

    open(path, "w") truncates the destination the moment it is opened, so a
    write that fails part way through (a full disk, a killed process) would
    leave a truncated sitemap on disk — exactly the broken file the validation
    above exists to prevent, and the workflow would commit and push it. The
    content is staged in a temporary file next to the destination and moved
    into place with os.replace(), which is atomic within one filesystem. On
    failure the staged file is removed and the previous, already validated
    sitemap stays untouched.
    """
    output_path = os.path.join(REPO_ROOT, filename)
    directory = os.path.dirname(output_path) or "."
    fd, tmp_path = tempfile.mkstemp(dir=directory, prefix=f"{filename}.", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as fh:
            fh.write(content)
            fh.flush()
            os.fsync(fh.fileno())
        os.replace(tmp_path, output_path)
    except BaseException:
        if os.path.exists(tmp_path):
            os.remove(tmp_path)
        raise
    return output_path


def main():
    sitemap = generate_sitemap()
    try:
        url_count = validate_sitemap(sitemap)
        index = generate_sitemap_index(sitemap)
        validate_sitemap_index(index)
    except ValueError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"{SITEMAP_FILENAME} written to {write_output(SITEMAP_FILENAME, sitemap)} ({url_count} URLs)")
    print(f"{SITEMAP_INDEX_FILENAME} written to {write_output(SITEMAP_INDEX_FILENAME, index)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
