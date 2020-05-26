using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityScript.Steps;

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
        if (Input.GetMouseButtonDown(2))
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

        if (Input.GetMouseButtonUp(2))
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
                int dualNgonId = (int)c.g - 1;
                int q = (int)c.b;
                int r = (int)c.a;
                worldGenerator.SetCursorAtVertex(vertId, dualNgonId, new TileAxialCoordinate(q, r, worldGenerator.divisions));
            }
            else
            {
                worldGenerator.HideCursor();
            }
        }
    }

    bool isRotating = false;
    Vector3 rotationStartPixel;
    Vector3 rotationPivot;
    float rotationDistance;
    float rotationTheta;
    float rotationPhi;

    void StartRotation()
    {
        if (isRotating) return;
        isRotating = true;
        rotationStartPixel = Input.mousePosition;

        // Rotate around plane point seen at camera center
        Ray ray = cam.ScreenPointToRay(new Vector2(Screen.width, Screen.height) / 2);
        rotationPivot = ray.origin - ray.direction * (ray.origin.y / ray.direction.y);
        Vector3 fromPivot = transform.position - rotationPivot;
        rotationDistance = fromPivot.magnitude;
        Vector3 d = fromPivot / rotationDistance;
        rotationTheta = Mathf.Acos(d.y);
        float sinTheta = Mathf.Sqrt(1 - d.y * d.y);
        rotationPhi = Mathf.Atan2(d.z / sinTheta, d.x / sinTheta);

        Debug.Assert(Mathf.Abs(d.x - Mathf.Cos(rotationPhi) * Mathf.Sin(rotationTheta)) < 1e-4, "error: " + d.x + " != " + (Mathf.Cos(rotationPhi) * Mathf.Sin(rotationTheta)));
        Debug.Assert(Mathf.Abs(d.y - Mathf.Cos(rotationTheta)) < 1e-4, "error: " + d.y + " != " + Mathf.Cos(rotationTheta));
        Debug.Assert(Mathf.Abs(d.z - Mathf.Sin(rotationPhi) * Mathf.Sin(rotationTheta)) < 1e-4, "error: " + d.z + " != " + (Mathf.Sin(rotationPhi) * Mathf.Sin(rotationTheta)));
        Debug.Assert(Vector3.Distance(transform.position, rotationPivot + fromPivot) < 1e-4);
        Debug.Assert(Vector3.Distance(fromPivot, d * rotationDistance) < 1e-4);
    }

    void UpdateRotation()
    {
        if (!isRotating) return;
        // Update angles
        Vector2 delta = Input.mousePosition - rotationStartPixel;
        float phi = rotationPhi - delta.x * 0.01f;
        float theta = Mathf.Max(0.01f, rotationTheta + delta.y * 0.01f);

        // Update position
        float sinTheta = Mathf.Sin(theta);
        Vector3 fromPivot = new Vector3(
            Mathf.Cos(phi) * sinTheta,
            Mathf.Cos(theta),
            Mathf.Sin(phi) * sinTheta
        ) * rotationDistance;
        transform.position = rotationPivot + fromPivot;

        transform.LookAt(rotationPivot);
    }

    void StopRotation()
    {
        if (!isRotating) return;
        isRotating = false;
    }

    void UpdateCameraRotation()
    {
        if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftAlt))
        {
            StartRotation();
        }
        UpdateRotation();
        if (Input.GetMouseButtonUp(0))
        {
            StopRotation();
        }
    }

    void Update()
    {
        UpdateCameraMove();
        UpdateCameraRotation();
        UpdateCursor();

        if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftAlt))
        {
            worldGenerator.AddVoxelAtCursor();
        }
        if (Input.GetMouseButtonDown(1))
        {
            worldGenerator.RemoveVoxelAtCursor();
        }
    }
}
