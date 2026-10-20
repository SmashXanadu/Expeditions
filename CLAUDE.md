# Project Instructions

## Writing style

- **Never use an em dash (—) anywhere in this project.** If a sentence wants one, restructure it (split into two sentences, use a colon, a semicolon, parentheses, or a comma) rather than swapping in a regular hyphen (`-`) as a stand-in. A regular hyphen is only correct for actual hyphenated words or numeric ranges.
- **Always write "Waystone" / "Waystones", capitalized.** Never "Weystone" or lowercase "waystone" in prose. URL anchors and permalinks (e.g. `#the-waystone-network`) stay lowercase.
- **All random tables are d20.** The game uses a single d20; never write d4/d6/etc. tables. Entries in print tables must fit on one line.

## Print layout checks

Booklets are 5.5"x8.5" PDFs built by `Tools/MarkdownPdfConverter.cs`. After editing any print-bound page, verify with `Tools/LayoutCheck/check.ps1` instead of guessing:

- `.\Tools\LayoutCheck\check.ps1 render "UnchartedWaters/Setting Guide/0[5-9]*.md"`: page count per file, plus PDFs and per-page PNGs in `%TEMP%\LayoutCheck`.
- `.\Tools\LayoutCheck\check.ps1 width "UnchartedWaters/Setting Guide/*.md"`: lists table cells after `\smalltables` that would wrap (default limit 312pt for `| # | Text |` tables).

Setting Guide Ch. 5-18 must each render as exactly 2 pages (content left, d20 tables right).
