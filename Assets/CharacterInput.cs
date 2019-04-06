using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public float moveAmountForward = 5;
    public float moveAmountSide = 3;
    public float interpAmount = .5f;
    public float jumpHeight = 6;
    private CharacterController cc;
    private Vector3 moveVector;
    public Mover mover;

    // Start is called before the first frame update
    void Start()
    {
        mover = GetComponent<Mover>();
        cc = GetComponent<CharacterController>();
        moveVector = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        mover.velocity -= moveVector;

        moveVector = Vector3.Lerp(moveVector, Input.GetAxis("Vertical") * transform.forward * moveAmountForward +
                                              Input.GetAxis("Horizontal") * transform.right * moveAmountSide, interpAmount);

        mover.velocity += moveVector;

        if (Input.GetButton("Jump") && cc.isGrounded)
        {
            float jumpVelocity = Mathf.Sqrt(-Gravity.gravity * 2 * jumpHeight);
            mover.velocity.y = 0;
            mover.velocity.y = jumpVelocity;
        }
    }
}
