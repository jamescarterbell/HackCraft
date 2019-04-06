using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraLook : MonoBehaviour
{
    Transform cam;

    public float xMoveThreshold = 1000.0f;
    public float yMoveThreshold = 1000.0f;

    public float yMaxLimit = 45.0f;
    public float yMinLimit = -45.0f;


    float yRotCounter = 0.0f;
    float xRotCounter = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        xRotCounter += Input.GetAxis("Mouse X") * xMoveThreshold * Time.deltaTime;
        yRotCounter += Input.GetAxis("Mouse Y") * yMoveThreshold * Time.deltaTime;
        yRotCounter = Mathf.Clamp(yRotCounter, yMinLimit, yMaxLimit);
        //xRotCounter = xRotCounter % 360;//Optional
        transform.localEulerAngles = new Vector3(0, xRotCounter, 0);

        cam.eulerAngles = new Vector3(-yRotCounter, xRotCounter, 0);
    }
}
