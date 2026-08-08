using UnityEngine;

namespace UTI
{
    /// <summary>Which space BeanMouseTracker writes the mouse cursor's position into.</summary>
    public enum BeanMouseTrackingSpace
    {
        Screen,
        World
    }

    // Deliberately a proxy, not a change to BeanTracker itself: this drives its own Transform to
    // follow the mouse every frame, so a normal BeanTracker on the same GameObject captures it
    // through the exact same pipeline (buffer, OnSample, BeanLogger, BeanVisualizer,
    // BeanSnapshotExporter) as any other tracked object, with zero changes needed anywhere else
    // in UTI. Uses the legacy Input Manager (Input.mousePosition), not the Input System package's
    // Mouse.current, specifically to avoid adding a hard package dependency - keeps UTI's "no
    // required dependencies" promise intact even for projects that don't have Input System
    // installed. If a project's Active Input Handling (Project Settings > Player) is set to
    // "Input System Package (New)" only, switch it to "Both" for this component to work.
    /// <summary>
    /// Attach alongside a BeanTracker to make it follow the mouse cursor instead of this
    /// GameObject's own gameplay movement - useful for debugging exact mouse input/aim behavior
    /// the same way any other Bean tracks a moving object.
    /// </summary>
    public class BeanMouseTracker : MonoBehaviour
    {
        [SerializeField] private BeanMouseTrackingSpace trackingSpace = BeanMouseTrackingSpace.Screen;
        // Only used in World mode - defaults to Camera.main if unset.
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float worldDistanceFromCamera = 10f;

        public BeanMouseTrackingSpace TrackingSpace { get => trackingSpace; set => trackingSpace = value; }
        public Camera WorldCamera { get => worldCamera; set => worldCamera = value; }
        public float WorldDistanceFromCamera { get => worldDistanceFromCamera; set => worldDistanceFromCamera = value; }

        private void Update()
        {
            Vector2 mouseScreenPosition = Input.mousePosition;

            transform.position = trackingSpace == BeanMouseTrackingSpace.World
                ? ResolveWorldPosition(mouseScreenPosition, worldCamera != null ? worldCamera : Camera.main, worldDistanceFromCamera)
                : ResolveScreenPosition(mouseScreenPosition);
        }

        /// <summary>
        /// Raw screen-space mouse position as a Vector3 (pixels, Z=0) - genre/camera-agnostic,
        /// works the same in any 2D or 3D project. Pure function, testable without Play Mode.
        /// </summary>
        public static Vector3 ResolveScreenPosition(Vector2 mouseScreenPosition) =>
            new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f);

        /// <summary>
        /// Projects the mouse's screen position into world space at a fixed distance in front of
        /// the given camera - useful for 3D aim-tracking (e.g. where a mouse-steered reticle
        /// actually points in the world). Returns Vector3.zero if no camera is available. Pure
        /// function of its inputs (ScreenToWorldPoint is a matrix calculation, not Play-Mode-only
        /// state), so it's testable without Play Mode.
        /// </summary>
        public static Vector3 ResolveWorldPosition(Vector2 mouseScreenPosition, Camera camera, float distanceFromCamera)
        {
            if (camera == null)
                return Vector3.zero;

            return camera.ScreenToWorldPoint(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, distanceFromCamera));
        }
    }
}
