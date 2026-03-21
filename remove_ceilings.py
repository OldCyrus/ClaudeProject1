"""
Removes all ceiling GameObjects and their components from ClaudeMainLevel.unity.
Also removes their fileIDs from parent Transform m_Children lists.
"""

import re, shutil, sys
from pathlib import Path

SCENE = Path(r"C:\Users\tubal\ClaudeProject1\Assets\ClaudeMainLevel.unity")
BACKUP = SCENE.with_suffix(".unity.bak")

# ── 1. Read raw file ──────────────────────────────────────────────────────────
text = SCENE.read_text(encoding="utf-8")

# ── 2. Split into YAML documents (keep the "--- !u!N &id" header) ─────────────
# Each block starts with "--- " on its own line.
blocks = re.split(r'(?=^--- )', text, flags=re.MULTILINE)
# blocks[0] is the file header ("%YAML 1.1\n%TAG ...")

# ── 3. Find all ceiling GameObject IDs and their component IDs ────────────────
ceiling_go_ids   = set()   # fileID of the GameObject
ceiling_comp_ids = set()   # fileID of every component it owns

for block in blocks:
    if re.search(r'm_Name:\s+\S*[Cc]eiling', block):
        # Extract this block's own fileID from the header line
        m = re.match(r'^--- !u!\d+ &(\d+)', block)
        if not m:
            continue
        go_id = m.group(1)
        ceiling_go_ids.add(go_id)
        ceiling_comp_ids.add(go_id)   # the GO itself is also a "block to remove"

        # Extract all component fileIDs
        for comp_id in re.findall(r'component: \{fileID: (\d+)\}', block):
            ceiling_comp_ids.add(comp_id)

print(f"Found {len(ceiling_go_ids)} ceiling GameObjects")
print(f"Total blocks to remove (GO + components): {len(ceiling_comp_ids)}")

# ── 4. Remove those blocks entirely ──────────────────────────────────────────
kept = []
removed_names = []

for block in blocks:
    m = re.match(r'^--- !u!\d+ &(\d+)', block)
    if m and m.group(1) in ceiling_comp_ids:
        # Grab name for reporting
        nm = re.search(r'm_Name:\s+(\S+)', block)
        if nm:
            removed_names.append(nm.group(1))
        continue   # drop this block
    kept.append(block)

# ── 5. Strip ceiling transform refs from parent m_Children lists ──────────────
result = "".join(kept)

for comp_id in ceiling_comp_ids:
    # Matches lines like:  - {fileID: 42170769}
    pattern = rf'\n  - \{{fileID: {comp_id}\}}'
    result = re.sub(pattern, '', result)

# ── 6. Write backup then updated scene ───────────────────────────────────────
shutil.copy(SCENE, BACKUP)
print(f"Backup saved: {BACKUP}")

SCENE.write_text(result, encoding="utf-8")
print(f"Scene written: {SCENE}")

# ── 7. Report ─────────────────────────────────────────────────────────────────
print(f"\nRemoved {len(ceiling_go_ids)} ceiling objects:")
for name in sorted(set(removed_names)):
    print(f"  • {name}")
