using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform debug;
    public float zoomSensitivity = 1;

    Camera cam;
    Vector3 draggedPoint;
    bool dragging = false;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    Ray ScreenPointToRay(Vector2 screenPos)
    {
        Vector2 clipPos = new Vector2(
            1 - (screenPos.x + 0.5f) / Screen.width * 2,
            1 - (screenPos.y + 0.5f) / Screen.height * 2
        );
        var m = (cam.projectionMatrix * cam.transform.worldToLocalMatrix).inverse;
        var direction = (m * new Vector4(clipPos.x, clipPos.y, 1, 1)).normalized;
        return new Ray(cam.transform.position, direction);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = ScreenPointToRay(Input.mousePosition);
            draggedPoint = ray.origin - ray.direction * (ray.origin.y / ray.direction.y);
            dragging = true;
            debug.position = draggedPoint;
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
}
