using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    private Camera cam;
    Vector3 lastDragPosition;
    float speed = 20;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
    }
    void UpdateDrag()
    {
        if (Input.GetMouseButtonDown(2))
            lastDragPosition = Input.mousePosition;
        if (Input.GetMouseButton(2))
        {
            var delta = lastDragPosition - Input.mousePosition;
            transform.Translate(delta * Time.deltaTime * 0.25f * 20);
            lastDragPosition = Input.mousePosition;
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateDrag();
        Vector3 pivotPointWorld = new Vector3(0,0,0);
         if (Input.GetMouseButtonDown (1)) {
            Vector3 pivotPointScreen = Input.mousePosition;
            Ray ray = cam.ScreenPointToRay(pivotPointScreen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
                pivotPointWorld = hit.point;
            Cursor.visible = false;
        }
        else if(Input.GetMouseButtonUp(1))
            Cursor.visible = true;
        if (Input.GetMouseButton (1)) {
            //Rotate the camera X wise
            float angularSpeed = 2;
            cam.transform.RotateAround(pivotPointWorld,Vector3.up, angularSpeed * Input.GetAxis ("Mouse X"));
            //Rotate the camera Y wise
            cam.transform.RotateAround(pivotPointWorld,Vector3.right, angularSpeed * Input.GetAxis ("Mouse Y"));
        }

        // also debugging purposes, for moving the camera
        if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(new Vector3(speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(new Vector3(-speed * Time.deltaTime,0,0));
        }
        if(Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(new Vector3(0,-speed * Time.deltaTime,0));
        }
        if(Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(new Vector3(0,speed * Time.deltaTime,0));
        }

        float zoomSpeed = 200;
        // Mouse wheel moving forwards
        var mouseScroll = Input.GetAxis("Mouse ScrollWheel");

        if (mouseScroll!=0)
        {
            transform.Translate(transform.forward * mouseScroll * zoomSpeed * Time.deltaTime, Space.Self);
        }
    }
}
