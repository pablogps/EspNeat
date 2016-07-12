using UnityEngine;
using System.Collections;

/// <summary>
/// This class allows to easily locate buttons and other UI elements in our
/// screen space canvas. This canvas uses a camera, so this objects can
/// be easily hidden. The position will be given as a fraction of the screen
/// size, but this class works with the split screen used in the main menu, 
/// so the user may choose if the position refers to the left or right
/// sub-screen.
/// </summary>
public class ScreenSpaceButtonStarter : MonoBehaviour {

    public bool isLeftSide;
    // Position will be given as a fraction of the local screen (left-side
    // or right-side screen), with lower-left corners as reference
    public float xPosition;
    public float yPosition;
    // The positions are relative to the screen size. xOffsetPixels allows to
    // offset this result by a given (absolute) number of pixels!
	public float xOffsetPixels;
	public float yOffsetPixels;

    /// <summary>
    /// The position of the button is done in Start, because in Awake the 
    /// canvas is not ready (reports size = 0)
    /// 
    /// 0,0 is at the centre for canvas, but the lower left corner for the viewport.
    /// </summary>
    void Start()
    {
        GameObject screenCanvas = GameObject.Find("ScreenSpaceCanvas");
        RectTransform canvasRect = screenCanvas.GetComponent<RectTransform>();
        Camera backgroundCamera = GameObject.Find("BackgroundCamera").GetComponent<Camera>();

        // We need the reference point where the two screens are split. For this
        // we use the right side (min x value) of the gameObject:
        GameObject solidBackground = GameObject.Find("SolidBackgroundToHideUI");
        // We use its renderer to find its size in world-reference
        Renderer renderer = solidBackground.GetComponent<Renderer>();

        Vector3 middlePoint = solidBackground.transform.position;
        // Note larger value of x means more to the left in our case!
        middlePoint.x = renderer.bounds.min.x;

        // Translates the position of this point into viewportPosition (that is,
        // position as a fraction of the screen size)
        Vector2 middlePointViewport = backgroundCamera.WorldToViewportPoint(middlePoint);

        // For the y coordinate we use yPosition. This is the same for both
        // screens!
        middlePointViewport.y = yPosition;

        // Gets the position of the division between both screens, in canvas
        // coordinates.
        // Including a correction for the fact that 0,0 is not the lower left
        // corner for the canvas.
        Vector2 middlePointCanvas = new Vector2(
                middlePointViewport.x * canvasRect.sizeDelta.x - canvasRect.sizeDelta.x * 0.5f,
                middlePointViewport.y * canvasRect.sizeDelta.y - canvasRect.sizeDelta.y * 0.5f);

        // It is a bit more clear if we use this extra vector (we could instead
        // modify middlePointCanvas). We copy middlePointCanvas because there
        // we already have the correct y value!
        Vector2 objectPositionCanvas = middlePointCanvas;

        // Now we need the x coordinate of our object, in canvas coordinates.
        // Calling A the left side, C the right side and B the division:
        // Finds the position in between A and B or B and C depending on
        // whether we want the left screen (A and B) or right screen (B and C).
        if (isLeftSide) {
            // Left side:  A + xPosition * (B - A)
            // A = -canvasRect.sizeDelta.x * 0.5f
            // B = middlePointCanvas.x

            objectPositionCanvas.x =
                    -canvasRect.sizeDelta.x * 0.5f +
                    xPosition * (middlePointCanvas.x + canvasRect.sizeDelta.x * 0.5f);
        }
        else
        {
            // Right-side screen: B + xPosition * (C - B)
            // B = middlePointCanvas.x
            // C = canvasRect.sizeDelta.x * 0.5f
            objectPositionCanvas.x =
                middlePointCanvas.x +
                xPosition * (canvasRect.sizeDelta.x * 0.5f - middlePointCanvas.x );
        }

        // Finally adds the desired offset:
		objectPositionCanvas.x += xOffsetPixels;
		objectPositionCanvas.y -= yOffsetPixels;

        transform.localPosition = objectPositionCanvas;         
    }
}
