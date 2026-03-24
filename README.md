# Camera Controllers

This is a collection of camera controllers using Unity's new Input System. All keybindings can be remapped
through the `InputActions` asset for each controller.

These camera controllers are designed to be modular and extensible so that you can easily drop them into any Unity
project for prototyping or to customise and extend them as the basis for your own gameplay systems.

## RTS Camera Controller

This overhead strategic camera controller combines elements of top down cameras found in games like Total War with zoom 
and pitch and yaw rotation and mouse drag for rotation and movement, as well as traditional RTS controls like edge 
panning and keyboard controls.

### Setup
This controller should be placed on a Camera Rig game object, which you should treat as the 'focus' of the camera.
The actual camera in the scene should be a child of this rig, and should be positioned at the desired default zoom
distance from the rig, with a rotation such that it is looking at the rig. The default values for these are then initialized
from this position whenever the game is launched.

There is an RTS Camera Rig prefab in the Prefabs forlder that you can drop in your scene.
Make sure to remove any duplicate cameras if you do this.

### Controls

- Keyboard:
  - `WASD` or `Arrow Keys` to pan the camera
  - `QE` to rotate the camera yaw
  - `Space` to reset pitch and yaw
- Mouse:
  - `Left Click + Drag` to pan the camera
  - `Right Click + Drag` to rotate the camera yaw
  - `Scroll Click + Drag` to rotate the camera pitch
  - `Scroll Wheel` to zoom in and out

You can also pan the camera by moving the mouse cursor to the edges of the screen.

All parameters of this camera are exposed in the inspector and customisable:
- Pan, yaw, pitch and zoom speeds
- Camera bounds, pitch limits, zoom limits
- Screen edge pan width and height zone

There is automatic pan speed adjustment based on the camera's zoom level, which is controlled by the 
`m_maxZoomSpeedMultiplier` - at max zoom the camera moves slower, and at min zoom the camera moves faster.
This is to ensure that the camera movement feels consistent regardless of the zoom level.

## Fly Camera Controller

This is a free flying camera controller, similar to the fly mode in the Unity Editor Scene view.

This camera locks the cursor to the game window and hides it when active, and allows the user to move freely in 3D space using keyboard controls and mouse look.

### Setup
You can place the `Fly Camera Controller` component on any game object in your scene and it will work as it fetches
the main camera at runtime. It's recommended to place it on the camera object for better organization.

### Controls
- Keyboard:
  - `WASD` or `Arrow Keys` to move the camera
  - `QE` to move the camera vertically
  - `Escape` to pause the camera and unlock the cursor. Pressing `Escape` again will resume the camera and lock the cursor.
  - `Left Shift` to boost movement speed
- Mouse:
    - Look around by moving the mouse. The camera's yaw and pitch will be adjusted based on the mouse movement.

## Orbit Camera Controller

This is a simple orbit camera controller that allows the user to orbit around a target point, zoom in and out, and adjust the pitch and yaw of the camera.
It can be combined with a character controller for a third person camera setup, or used on its own to demo a scene or object.

### Controls
- Keyboard:
  - `Space` to reset zoom and rotation
- Mouse:
  - `Right Click + Drag` to orbit the camera around the target point
  - `Scroll` to adjust the camera's zoom