using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gravity : MonoBehaviour
{
    public static float gravity = -20;

    private Mover mover;
    private CharacterController cc;

    // Start is called before the first frame update
    void Start()
    {
        mover = GetComponent<Mover>();
        cc = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
         mover.velocity.y += gravity * Time.deltaTime;
    }
}
