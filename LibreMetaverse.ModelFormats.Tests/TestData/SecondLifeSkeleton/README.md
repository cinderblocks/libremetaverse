# Second Life skeleton test files

Rigged avatar meshes (head, body, hair, eyes, eyelashes, skirt) weighted to the Second Life skeleton, exported
from Blender 4.5 as COLLADA (`.dae`) and glTF (`.glb`). They are the test input for rigged mesh import in
`LibreMetaverse.ModelFormats`. See `SecondLifeSkeletonFileTests`.

- `default-blender-all-bones`: the skeleton with its deform (`m`), attachment (`a`) and volume bones
- `default-blender-deform-bones`: the skeleton with its deform bones only

- `sl-open-sim-rigged.dae`: the same scene exported for Second Life, from `second-life-skeleton.blend` with Blender
  4.5.10, COLLADA export, the `sl+open sim rigged` preset, `Apply` under Global Orientation, `Forward Axis: -X`,
  `Up Axis: Z`. The avatar faces +X. (Every bone of the blend file is a deform bone, so there is one file, not two.)

The `default-blender-*` files face -Y, as Blender draws a character, and are not oriented for Second Life.

Source: https://github.com/LittleBitwise/Second-Life-Skeleton (commit `a38a64f`). Used in LibreMetaverse with the
permission of the repository's owner.
