#!/usr/bin/env python3
"""
Generate sitemap.xml for s-iguchi09.github.io.

This script scans all HTML files in the repository root,
builds a multilingual sitemap with hreflang annotations
for English and Japanese pages, and writes sitemap.xml.

URL structure:
  - English pages: https://s-iguchi09.github.io/<path>
  - Japanese pages: https://s-iguchi09.github.io/ja/<path>
  - index.html  → trailing-slash URL  (e.g. / or /ja/)
"""

import os
import re
import glob
import datetime
import subprocess
from typing import Optional

BASE_URL = "https://s-iguchi09.github.io"
REPO_ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
TODAY = datetime.date.today().isoformat()

# Directories to exclude from scanning
EXCLUDE_DIRS = {"_includes", "_layouts", "_site", ".github", ".git"}

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


def extract_images_from_file(abs_path: str) -> list:
    """Return a list of absolute image URLs found in an HTML or Markdown file.

    For HTML files, looks for <img src="..."> tags.
    For Markdown files, looks for ![...](src) syntax.
    Only site-relative paths (starting with /) are included.
    """
    if not os.path.isfile(abs_path):
        return []
    try:
        with open(abs_path, encoding="utf-8", errors="replace") as fh:
            content = fh.read()
    except OSError:
        return []

    images = []
    if abs_path.endswith(".md"):
        # Markdown image syntax: ![alt](path)
        for src in re.findall(r'!\[[^\]]*\]\(([^)]+)\)', content):
            src = src.strip().split()[0]  # strip optional title (e.g. "url title")
            if src.startswith("/"):
                images.append(BASE_URL + src)
    else:
        # HTML <img src="..."> (also handles single quotes)
        for src in re.findall(r'<img\s[^>]*\bsrc=["\']([^"\']+)["\']', content, re.IGNORECASE):
            if src.startswith("/"):
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
        lines.append(f"    <image:image><image:loc>{img_url}</image:loc></image:image>")
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
            lines.append(f"    <image:image><image:loc>{img_url}</image:loc></image:image>")
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
        lines.append(f"    <image:image><image:loc>{img_url}</image:loc></image:image>")
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
            lines.append(f"    <image:image><image:loc>{img_url}</image:loc></image:image>")
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


def main():
    sitemap = generate_sitemap()
    output_path = os.path.join(REPO_ROOT, "sitemap.xml")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(sitemap)
    print(f"sitemap.xml written to {output_path}")


if __name__ == "__main__":
    main()
