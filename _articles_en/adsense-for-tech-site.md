---
layout: article-en
title: "Improving a Tech Site for Google AdSense Review"
date: 2026-04-10
category: Site
excerpt: "Practical improvements I made to pass the Google AdSense review — content structure, navigation, and how to demonstrate originality."
---

## Background

After building a Windows application showcase site, I applied for Google AdSense and received a feedback message about insufficient content. This article documents the concrete steps I took to address that feedback and eventually pass the review.

## Key Areas for Improvement

### 1. Add Original, In-Depth Articles

AdSense reviewers look for unique, helpful content that goes beyond simple app listings. I added a dedicated **Articles** section (`/articles/`) with technical write-ups such as:

- How-to guides for specific WPF controls
- Comparison of implementation patterns
- Notes from my own development experience

Each article aims for at least 600–800 words with code examples, tables, and clear section headings.

### 2. Improve Navigation Structure

The original header contained only About, Privacy Policy, and Contact links. I added:

- **Apps** — direct link to the application list
- **Articles** — new section for technical content

This makes it immediately clear to reviewers (and users) that the site has multiple content categories.

### 3. Show Content Freshness on the Home Page

I added a **Latest Articles** section to the home page that automatically renders the three most recent articles using a Jekyll Liquid loop. This:

- Demonstrates that the site is actively maintained
- Gives visitors an immediate preview of technical depth
- Eliminates the need to manually update the home page on every new post

### 4. Strengthen the Privacy Policy Page

I expanded the Privacy Policy to explicitly mention:

- Use of Google AdSense and the DoubleClick cookie
- Use of Google Analytics
- How visitors can opt out

A clear, detailed Privacy Policy is a hard requirement for AdSense approval.

### 5. Clean Up Internal Links

I audited all internal links to ensure none pointed to 404 pages. I also added canonical URLs and `hreflang` attributes for the bilingual (EN/JA) setup.

## Result

After these changes the site passed the AdSense review within two weeks. The most impactful changes were (1) adding original technical articles and (2) surfacing them prominently on the home page.

## Checklist

- [ ] Add at least 3 original articles with 600+ words each
- [ ] Link to articles from the home page and nav
- [ ] Write a detailed Privacy Policy covering AdSense / Analytics
- [ ] Fix all broken internal links
- [ ] Add canonical and hreflang tags for multilingual pages
- [ ] Verify site is mobile-friendly
