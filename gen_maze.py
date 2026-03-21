"""
Generates an 8x10 grid maze with:
- Spanning tree via DFS (every cell reachable)
- Extra random edges for loops
- Outputs C# arrays for BuildPrisonLevel.cs
"""
import random, sys
random.seed(42)

COLS, ROWS = 8, 10

# Adjacency: for each cell list (dc,dr) neighbours
def neighbours(c, r):
    result = []
    if c > 0:       result.append((c-1, r, 'W'))
    if c < COLS-1:  result.append((c+1, r, 'E'))
    if r > 0:       result.append((c, r-1, 'N'))
    if r < ROWS-1:  result.append((c, r+1, 'S'))
    return result

# vOpen[c][r] = passage between (c,r) and (c+1,r)   c: 0..6
# hOpen[c][r] = passage between (c,r) and (c,r+1)   r: 0..8
vOpen = [[False]*ROWS for _ in range(COLS-1)]
hOpen = [[False]*(ROWS-1) for _ in range(COLS)]

def open_passage(c1,r1, c2,r2):
    if c1 == c2:   # vertical move — horizontal wall between them
        r = min(r1,r2)
        hOpen[c1][r] = True
    else:          # horizontal move — vertical wall between them
        c = min(c1,c2)
        vOpen[c][r1] = True

# DFS spanning tree from (2,0) — left entry column
visited = [[False]*ROWS for _ in range(COLS)]
stack = [(2,0)]
visited[2][0] = True
while stack:
    c,r = stack[-1]
    nbrs = [(nc,nr,d) for nc,nr,d in neighbours(c,r) if not visited[nc][nr]]
    if not nbrs:
        stack.pop()
        continue
    nc,nr,d = random.choice(nbrs)
    open_passage(c,r, nc,nr)
    visited[nc][nr] = True
    stack.append((nc,nr))

# Make sure right-entry column (col 5, row 0) has a north opening
# (it's already in the tree, we just need to note it as an entry)
# Add ~25% extra passages for loops
all_walls = []
for c in range(COLS-1):
    for r in range(ROWS):
        if not vOpen[c][r]:
            all_walls.append(('v', c, r))
for c in range(COLS):
    for r in range(ROWS-1):
        if not hOpen[c][r]:
            all_walls.append(('h', c, r))

random.shuffle(all_walls)
extra = len(all_walls) // 4
for w in all_walls[:extra]:
    t,c,r = w
    if t == 'v': vOpen[c][r] = True
    else:         hOpen[c][r] = True

# Verify all cells reachable from entry
def bfs(start_cells):
    visited = [[False]*ROWS for _ in range(COLS)]
    queue = list(start_cells)
    for c,r in queue:
        visited[c][r] = True
    while queue:
        c,r = queue.pop(0)
        # east
        if c < COLS-1 and vOpen[c][r] and not visited[c+1][r]:
            visited[c+1][r] = True; queue.append((c+1,r))
        # west
        if c > 0 and vOpen[c-1][r] and not visited[c-1][r]:
            visited[c-1][r] = True; queue.append((c-1,r))
        # south
        if r < ROWS-1 and hOpen[c][r] and not visited[c][r+1]:
            visited[c][r+1] = True; queue.append((c,r+1))
        # north
        if r > 0 and hOpen[c][r-1] and not visited[c][r-1]:
            visited[c][r-1] = True; queue.append((c,r-1))
    return sum(visited[c][r] for c in range(COLS) for r in range(ROWS))

reachable = bfs([(2,0),(5,0)])
print(f"Reachable cells from entries: {reachable}/{COLS*ROWS}")

# Count dead ends (cells with exactly 1 passage)
dead_ends = []
for c in range(COLS):
    for r in range(ROWS):
        count = 0
        if c < COLS-1 and vOpen[c][r]: count += 1
        if c > 0 and vOpen[c-1][r]: count += 1
        if r < ROWS-1 and hOpen[c][r]: count += 1
        if r > 0 and hOpen[c][r-1]: count += 1
        if r == 0: count += 1  # top entries from hallway (col 2 and 5)
        if count == 1:
            dead_ends.append((c,r))

print(f"Dead ends: {len(dead_ends)}: {dead_ends}")
print()

# Rooms: 12 dead-end or near-dead-end cells
# Prefer deep cells (high r) and distributed across X
import math
def score(c,r):
    # Prefer deep, distributed, dead-end
    depth_score = r
    is_dead = 1 if (c,r) in dead_ends else 0
    return depth_score * 2 + is_dead

room_candidates = sorted([(c,r) for c in range(COLS) for r in range(ROWS)],
                          key=lambda cr: -score(cr[0],cr[1]))

# Pick 12 rooms spread across columns
chosen_rooms = []
col_used = set()
for c,r in room_candidates:
    if len(chosen_rooms) >= 12:
        break
    # Ensure distribution
    if c not in col_used or len(chosen_rooms) > 4:
        chosen_rooms.append((c,r))
        col_used.add(c)

print(f"Rooms initial: {chosen_rooms}")

# Better room selection: ensure spread across all rows
# Pick 12 rooms, one from each "zone" of 2-3 rows
zone_rooms = []
zone_size = ROWS // 4  # 2-3 rows per zone
for zone in range(4):
    r_start = zone * (ROWS // 4)
    r_end   = r_start + (ROWS // 4) + (2 if zone < 2 else 1)
    candidates = [(c,r) for c in range(COLS) for r in range(r_start, min(r_end, ROWS))]
    random.shuffle(candidates)
    added = 0
    cols_in_zone = set()
    for c,r in candidates:
        if added >= 3: break
        if c not in cols_in_zone:
            zone_rooms.append((c,r))
            cols_in_zone.add(c)
            added += 1

# Fill up to 12 from remaining
remaining = [(c,r) for c in range(COLS) for r in range(ROWS)
             if (c,r) not in zone_rooms and r >= 5]
random.shuffle(remaining)
zone_rooms.extend(remaining[:max(0, 12 - len(zone_rooms))])
chosen_rooms = zone_rooms[:12]

print(f"Rooms (12): {chosen_rooms}")

# Key spawn: deepest possible room far from cells (high row, prefer high col)
key_candidates = sorted([(c,r) for c,r in chosen_rooms], key=lambda cr: (cr[1]*8+cr[0]), reverse=True)
key_cell = key_candidates[0]
print(f"Key cell: {key_cell}")

# Exit door: middle rows of maze
exit_cell = (3, 4)
print(f"Exit cell: {exit_cell}")

# Enemy spawns: 8 spread throughout
enemy_cells = []
step = COLS * ROWS // 9
all_cells = [(c,r) for r in range(ROWS) for c in range(COLS)]
random.shuffle(all_cells)
skip = {key_cell, exit_cell}
for cc,rr in all_cells:
    if (cc,rr) not in skip and len(enemy_cells) < 8:
        enemy_cells.append((cc,rr))
print(f"Enemy spawns: {enemy_cells}")

# ── Output C# ─────────────────────────────────────────────────────────────────
print()
print("// Paste into BuildPrisonLevel.cs")
print()

# vOpen array (7 x 10)
print("        bool[,] vOpen = {")
for c in range(COLS-1):
    row_vals = ", ".join("true" if vOpen[c][r] else "false" for r in range(ROWS))
    print(f"            /* c={c} */ {{ {row_vals} }},")
print("        };")
print()

# hOpen array (8 x 9)
print("        bool[,] hOpen = {")
for c in range(COLS):
    row_vals = ", ".join("true" if hOpen[c][r] else "false" for r in range(ROWS-1))
    print(f"            /* c={c} */ {{ {row_vals} }},")
print("        };")
print()

# Entry columns
print(f"        // Hallway entries: top of col 2 and col 5")
print()

# Room cells
print("        var roomCells = new HashSet<(int,int)> {")
for c,r in chosen_rooms:
    print(f"            ({c},{r}),")
print("        };")
print()

print(f"        var keyCell   = ({key_cell[0]}, {key_cell[1]});")
print(f"        var exitCell  = ({exit_cell[0]}, {exit_cell[1]});")
print()

print("        var enemySpawnCells = new (int c, int r)[] {")
for c,r in enemy_cells:
    print(f"            ({c}, {r}),")
print("        };")
