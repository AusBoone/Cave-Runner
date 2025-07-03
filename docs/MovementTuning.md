# Movement Tuning

This document describes the key fields on `PlayerController` that affect how the character moves.

| Field | Description |
| --- | --- |
| `jumpForce` | Base impulse applied when a jump begins. |
| `variableJumpTime` | Duration the jump force continues while the button is held. Shorten for tighter hops. |
| `coyoteTime` | Grace period after leaving the ground where a jump is still allowed. |
| `jumpBufferTime` | Window before landing where a jump button press is remembered. |
| `slideBufferTime` | Similar buffer for slide input while in the air. |
| `airDiveForce` | Additional downward velocity when sliding mid‑air. |
| `fallGravityMultiplier` | Multiplier applied to gravity when descending. |
| `lowJumpGravityMultiplier` | Extra gravity applied when the jump key is released early. |
| `fastFallGravityMultiplier` | Further multiplier used when holding the down key in the air. |
| `slideDuration` | Maximum time the slide lasts. Releasing the key early ends the slide sooner. |
| `dashForce` | Horizontal impulse of the air dash triggered with slide. |
| `dashCooldown` | Minimum interval between consecutive air dashes. |

Adjust these values in the Unity inspector to dial in the desired feel. Higher gravity multipliers make movement snappier but can reduce hang time. Buffer times allow for more forgiving inputs.
