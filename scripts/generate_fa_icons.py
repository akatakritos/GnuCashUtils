#!/usr/bin/env python3
"""
Downloads Font Awesome 7 free icon metadata and generates FontAwesomeIcons.cs.

Usage:
    python3 scripts/generate_fa_icons.py

Output: GnuCashUtils/FontAwesomeIcons.cs
"""

import urllib.request
import os

FA7_ICONS_YML_URL = "https://raw.githubusercontent.com/FortAwesome/Font-Awesome/7.x/metadata/icons.yml"
OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "..", "GnuCashUtils", "FontAwesomeIcons.cs")

print(f"Downloading {FA7_ICONS_YML_URL} ...")
with urllib.request.urlopen(FA7_ICONS_YML_URL) as response:
    content = response.read().decode("utf-8")
print(f"Downloaded {len(content)} bytes")

# Parse the YAML manually — each top-level key is an icon name.
# Keys and unicode values may be single-quoted (e.g. '0': or unicode: '30').
icons = []
current_icon = None
current_unicode = None

for line in content.splitlines():
    # Top-level icon name: no leading spaces, ends with colon, not a comment
    if line and not line.startswith(" ") and not line.startswith("#") and line.endswith(":"):
        if current_icon and current_unicode:
            icons.append((current_icon, current_unicode))
        # Strip surrounding quotes from the key
        current_icon = line[:-1].strip("'\"")
        current_unicode = None
    elif line.strip().startswith("unicode:"):
        raw = line.split(":", 1)[1].strip().strip("'\"")
        current_unicode = raw

if current_icon and current_unicode:
    icons.append((current_icon, current_unicode))

print(f"Parsed {len(icons)} icons")


def to_pascal(name: str) -> str:
    parts = name.replace("-", "_").split("_")
    result = "".join(p.capitalize() for p in parts if p)
    if result and result[0].isdigit():
        result = "_" + result
    return result


def to_unicode_escape(hex_val: str) -> str:
    code_point = int(hex_val, 16)
    if code_point <= 0xFFFF:
        return f"\\u{code_point:04x}"
    else:
        return f"\\U{code_point:08x}"


lines = [
    "namespace GnuCashUtils;",
    "",
    "/// <summary>",
    "/// Unicode characters for Font Awesome 7 Free icons.",
    "/// Use with FontFamily=\"{StaticResource FontAwesomeIcons}\".",
    "/// Set FontWeight=Black for solid icons, FontWeight=Regular for regular icons.",
    "/// </summary>",
    "public static class FontAwesomeIcons",
    "{",
]

# Names that clash with object members and need the 'new' keyword
OBJECT_MEMBERS = {"Equals", "GetHashCode", "GetType", "ToString", "MemberwiseClone", "Finalize"}

for icon_name, unicode_val in sorted(icons, key=lambda x: x[0]):
    prop_name = to_pascal(icon_name)
    escape = to_unicode_escape(unicode_val)
    modifier = "new " if prop_name in OBJECT_MEMBERS else ""
    lines.append(f'    /// <summary>{icon_name}</summary>')
    lines.append(f'    public {modifier}const string {prop_name} = "{escape}";')

lines.append("}")

output = "\n".join(lines) + "\n"

output_path = os.path.normpath(OUTPUT_PATH)
with open(output_path, "w") as f:
    f.write(output)

print(f"Written to {output_path}")
