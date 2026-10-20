# `DisplacementTo`

## Purpose

Measure target-minus-self displacement in world XY and check independently enabled X/Y thresholds. All enabled comparisons must pass.

## Key inputs / outputs

- `object`: required target game object.
- `compareX`, `compareY`: enable each axis comparison.
- `absoluteX`, `absoluteY`: use the absolute axis value for comparison only.
- `xMode`, `yMode`: numeric comparison operators.
- `xExpect`, `yExpect`: constant or variable thresholds, read only for enabled comparisons.
- `storeResult`, `result`: optionally store one raw signed `Vector2`. The output is never normalized or made absolute.

The node uses the shared determine inspector. For a ground melee check, enable both comparisons, use absolute X/Y, and compare X against the melee range and Y against the height tolerance. For a target-above jump check, use absolute X but signed Y with `yMode = greater`.

## Success / Failure semantics

- A missing target binding follows the tree's node exception rule.
- A bound null or destroyed target returns failure without writing the output.
- With a valid target, success means every enabled axis comparison passed. No enabled comparisons means success.
- When storage is enabled and an output is bound, a valid target's displacement is stored even when a comparison fails.
- Each execution reads current positions and enabled thresholds before writing the output.
- Zero displacement is valid; it is compared normally.

## Important limitations

- Positions are Transform world positions. Z is ignored; this is not collider-gap distance.
- Only AND aggregation is supported. There are no per-axis outputs or magnitude comparisons.
- This unreleased node's earlier draft used nested `x`/`y`/`magnitude` settings and aggregation modes. Draft assets must explicitly reauthor their settings through the Editor before use. They are not migrated automatically.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/DisplacementTo.cs)
