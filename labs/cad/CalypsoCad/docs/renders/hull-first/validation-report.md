# Hull-first transverse skeleton validation

Overall: **PASS**

| Frame asms | BH asms | Expected BH | Dup frame@BH |
| --- | --- | --- | --- |
| 12 | 6 | 6 | 0 |

| Check | Result | Detail |
| --- | --- | --- |
| hull meshes present | PASS | oml/iml |
| ordinary frame assemblies | PASS | ringed=12 |
| heavy BH assemblies match schedule | PASS | bh=6 |
| no duplicate ordinary frame at BH station | PASS | dups=0 |
| BH stations have structural section | PASS | scheduled=6 |
| frames have perimeter rings | PASS | assembliesWithoutRing=0 |
