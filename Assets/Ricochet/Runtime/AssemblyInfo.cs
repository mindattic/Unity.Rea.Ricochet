// Lets the EditMode tests (Ricochet.EditorTests) see GameInput and InputBindings (both internal: no other assembly,
// and no save file, needs them) so UR-T36's action-map test can drive real key presses through the same static
// surface the game reads every frame.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ricochet.EditorTests")]
