using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    CharacterController cc;
    public Vector3 velocity;
    public Vector3 acceleration;
    public float downCheck;

    private float fallSpeedLimit = 50;
    private float lateralSpeedLimit = 50;

    // Start is called before the first frame update
    void Start()
    {
        cc = GetComponent<CharacterController>();   
    }

    // Update is called once per frame
    void Update()
    {
        velocity += acceleration * Time.deltaTime;
        if (cc.isGrounded && velocity.y <= 0)
        {
            velocity.y = 0;
            acceleration.y = 0;
        }
        Vector3 latSpeed = velocity - velocity.y * transform.up;
        if (Mathf.Abs(velocity.y) > fallSpeedLimit)
        {
            Vector3 fallSpeedNew = Mathf.Clamp(velocity.y, -fallSpeedLimit, fallSpeedLimit) * transform.up;
            velocity = velocity - velocity.y * transform.up + fallSpeedNew;
        }
        if(latSpeed.magnitude > lateralSpeedLimit)
        {
            velocity = velocity - latSpeed + Vector3.ClampMagnitude(latSpeed, lateralSpeedLimit);
        }

        cc.Move(velocity * Time.deltaTime);
    }
}
