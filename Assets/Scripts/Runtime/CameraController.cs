using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public WorldGenerator worldGenerator;
    public float zoomSensitivity = 1;
    public RenderTexture raycastRt;

    Camera cam;
    Vector3 draggedPoint;
    bool dragging = false;
    Texture2D raycastTexture;

    void Start()
    {
        cam = GetComponent<Camera>();
        raycastTexture = new Texture2D(1, 1, TextureFormat.RGBAHalf, false);
    }

    void UpdateCameraMove()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            draggedPoint = ray.origin - ray.direction * (ray.origin.y / ray.direction.y);
            dragging = true;
        }

        if (dragging)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            transform.position = draggedPoint + ray.direction * (transform.position.y / ray.direction.y);
        }

        if (Input.GetMouseButtonUp(1))
        {
            dragging = false;
        }

        transform.position = transform.position + transform.forward * Input.mouseScrollDelta.y * zoomSensitivity;
    }

    void UpdateCursor()
    {
        // Query pixel in rendering of a control mesh dedicated to mouse ray casting
        RenderTexture.active = raycastRt;
        float x = Input.mousePosition.x / (float)Screen.width;
        float y = 1 - Input.mousePosition.y / (float)Screen.height;
        if (x < 0 || x >= 1 || y < 0 || y >= 1)
        {
            worldGenerator.HideCursor();
        }
        else
        {
            raycastTexture.ReadPixels(new Rect(raycastRt.width * x, raycastRt.height * y, 1, 1), 0, 0);
            raycastTexture.Apply();
            Color c = raycastTexture.GetPixel(0, 0);
            if (c.g > 0)
            {
                int vertId = (int)c.r;
                int q = (int)c.b;
                int r = (int)c.a;
                worldGenerator.SetCursorAtVertex(vertId, new TileAxialCoordinate(q, r, worldGenerator.divisions));
            }
            else
            {
                worldGenerator.HideCursor();
            }
        }
    }

    void Update()
    {
        UpdateCameraMove();
        UpdateCursor();
    }
}
