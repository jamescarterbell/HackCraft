using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;

public class ChunkManager : MonoBehaviour
{
    public static Dictionary<int3,Chunk> chunks;
    public static int chunkSize = 16;
    public static float cubeSize = 1;

    // Start is called before the first frame update
    void Start()
    {
        chunks = new Dictionary<int3, Chunk>();
        Chunk[] chunksInScene = FindObjectsOfType<Chunk>();
        
        for(int i = 0; i < chunksInScene.Length; i++)
        {
            chunks.Add(vecToInt3(chunksInScene[i].transform.position), chunksInScene[i]);
        }

        generateCave();
    }

    private int3 vecToInt3(Vector3 coord)
    {
        return new int3((int)(((coord.x < 0) ? chunkSize + coord.x : coord.x) / (chunkSize * cubeSize)),
                        (int)(((coord.y < 0) ? chunkSize + coord.y : coord.y) / (chunkSize * cubeSize)),
                        (int)(((coord.z < 0) ? chunkSize + coord.z : coord.z) / (chunkSize * cubeSize)));
    }

    private Vector3 int3ToVec(int3 coord)
    {
        return new Vector3(coord.x * chunkSize * cubeSize,
                           coord.y * chunkSize * cubeSize,
                           coord.z * chunkSize * cubeSize);
    }

    public void placeBlock(Vector3 coord, short block)
    {
        int3 chunkCheck = vecToInt3(coord);
        if (chunks.ContainsKey(chunkCheck))
        {
            chunks[chunkCheck].placeBlock(coord, block);
        }
        else
        {
            GameObject g = new GameObject();
            g.name = "Chunk_" + chunkCheck.x.ToString() + "."
                              + chunkCheck.y.ToString() + "."
                              + chunkCheck.z.ToString();
            Chunk newChunk = g.AddComponent<Chunk>();
            newChunk.transform.position = int3ToVec(chunkCheck);
            newChunk.placeBlock(coord, block);
            chunks.Add(chunkCheck, newChunk);
        }
    }

    public void generateCave()
    {
        for(int x = 0; x < 100; x++)
        {
            for(int y = 0; y < 100; y++)
            {
                for(int z = 0; z < 100; z++)
                {
                    if(Mathf.Abs(x) < 3 && Mathf.Abs(y) < 3 && Mathf.Abs(z) < 3)
                    {
                        continue;
                    }

                    if (Perlin3D(x + .04f , y + .04f , z + .04f, .1f) < .5f)
                    {
                        placeBlock(new Vector3(x, y, z), 2);
                    }
                }
            }
        }
    }

    private float Perlin3D(float x, float y, float z, float scale=1)
    {
        x *= scale;
        y *= scale;
        z *= scale;
        float AB = Mathf.PerlinNoise(x, y);
        float AC = Mathf.PerlinNoise(x, z);
        float BC = Mathf.PerlinNoise(y, z);
        float BA = Mathf.PerlinNoise(y, x);
        float CA = Mathf.PerlinNoise(z, x);
        float CB = Mathf.PerlinNoise(z, y);

        return (AB + AC + BC + BA + CA + CB)/6;
    }
}
