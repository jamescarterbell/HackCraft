using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockAdder : MonoBehaviour
{
    public float range = 3;
    public LayerMask lm;

    private ChunkManager cm;

    // Start is called before the first frame update
    void Start()
    {
        cm = FindObjectOfType<ChunkManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlaceBlock(2);
        }

        if (Input.GetMouseButton(1))
        {
            RemoveBlock();
        }
    }

    public void PlaceBlock(int block)
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, range, lm))
        {
            Chunk c = hit.transform.GetComponent<Chunk>();
            if (c)
            {
                Vector3 blockPosition = hit.point;
                blockPosition += .25f * hit.normal;
                

                cm.placeBlock(blockPosition, 2);
            }
        }
    }

    public void RemoveBlock()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, range, lm))
        {
            Chunk c = hit.transform.GetComponent<Chunk>();
            if (c)
            {
                Vector3 blockPosition = hit.point;
                blockPosition -= .25f * hit.normal;
                

                cm.placeBlock(blockPosition, 0);
            }
        }
    }
}