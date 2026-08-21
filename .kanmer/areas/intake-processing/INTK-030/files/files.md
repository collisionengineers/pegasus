# Files

Committed in `ad1ba223`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/InstructionEvidenceImages.cs` | `MaximumPhotographSideRatio = 3.0` and `IsPhotographShaped`, applied in `Select` | the asset dimensions the reader already captures |
| `tests/Pegasus.Core.Tests/Intake/InstructionEvidenceImagesTests.cs` | The two measured banners excluded, the nine measured photographs kept, and an image with no recorded dimensions still admitted | existing selection tests |

## Nothing was added to capture

`WidthInSamples` and the bounding box were already recorded on every asset. This reads
what was there.
