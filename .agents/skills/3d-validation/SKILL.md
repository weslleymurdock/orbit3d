# 3D Validation Skill

## Use when
Validating a 3D milestone, sample, renderer or release candidate.

## Required reading
- `AGENTS.md`
- `docs/testing.md`
- `docs/performance.md`

## Checklist
1. Build affected engine projects.
2. Run backend-independent tests.
3. Validate the existing 2D path.
4. Validate the 3D sample with a representative asset.
5. Validate every claimed MAUI platform.
6. Check resource disposal and graphics-context recreation.
7. Check frame metrics and absence of obvious per-frame allocations.
8. Record unsupported configurations explicitly.

Do not declare a platform supported based only on compilation.
