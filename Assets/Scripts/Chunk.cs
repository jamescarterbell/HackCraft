using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[RequireComponent (typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    private short[] blocks;
    private Dictionary<short,int> types;
    public MeshFilter mf;
    public MeshCollider mc;
    public MeshRenderer mr;
    public bool createFull = false;

    public bool updateCheck;
    
    // Start is called before the first frame update
    void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("Chunks");
        blocks = new short[ChunkManager.chunkSize * ChunkManager.chunkSize * ChunkManager.chunkSize];
        mf = GetComponent<MeshFilter>();
        mc = GetComponent<MeshCollider>();
        mr = GetComponent<MeshRenderer>();

        types = new Dictionary<short, int>();

        if (createFull)
        {
            fullCreate();
        }

        ChunkUpdate();
    }

    public void fullCreate()
    {
        for(int i = 0; i < blocks.Length; i++)
        {
            placeBlock(i, 1);
        }
    }

    private void Update()
    {
        if (updateCheck){
            ChunkUpdate();
        }
    }

    public void placeBlock(Vector3 coord, short block)
    {
        coord -= transform.position;
        int coordI = CoordToArray(coord, ChunkManager.chunkSize);
        if (coordI < blocks.Length)
        {
            if (block == 0)
            {
                if (types.ContainsKey(blocks[coordI]))
                {
                    if (0 > --types[blocks[coordI]])
                    {
                        types.Remove(blocks[coordI]);
                    }
                }
            }
            else
            {
                if (!types.ContainsKey(block))
                {
                    types.Add(block, 1);
                }
                else
                {
                    types[block]++;
                }
            }
            blocks[coordI] = block;
            updateCheck = true;
        }
    }

    public void placeBlock(int coord, short block)
    {
        if (coord < blocks.Length)
        {
            if (block == 0)
            {
                if (types.ContainsKey(blocks[coord]))
                {
                    if(0 > --types[blocks[coord]])
                    {
                        types.Remove(blocks[coord]);
                    }
                }
            }
            else
            {
                if (!types.ContainsKey(block))
                {
                    types.Add(block, 1);
                }
                else
                {
                    types[block]++;
                }
            }
            blocks[coord] = block;
            updateCheck = true;
        }
    }

    public Vector3 ArrayToCoord(int i, int size)
    {
        return new Vector3
        {
            x = i % size,
            y = Mathf.FloorToInt(i / size) % size,
            z = Mathf.FloorToInt(i / (size * size))
        };
    }

    public int CoordToArray(Vector3 coord, int size)
    {
        return Mathf.Abs((int)(coord.z/ChunkManager.cubeSize) * size * size + (int)(coord.y/ChunkManager.cubeSize) * size + (int)(coord.x/ChunkManager.cubeSize));
    }

    private void ChunkUpdate()
    {
        updateCheck = false;
        var chunkClasses = new JUpdateChunk
        {
            _blocks = new NativeArray<short>(blocks, Allocator.Persistent),
            size = ChunkManager.chunkSize,
            _classes = new NativeArray<byte3>(blocks.Length, Allocator.Persistent)
        };

        JobHandle classHandle = chunkClasses.Schedule<JUpdateChunk>(blocks.Length, ChunkManager.chunkSize);

        JobHandle.ScheduleBatchedJobs();

        classHandle.Complete();
        

        int numberOfTris = 0, numberOfVerts = 0;

        foreach(byte3 b in chunkClasses._classes)
        {
            numberOfTris += b.y;
            numberOfVerts += b.z;
        }
        var meshInfo = new JGenerateMesh
        {
            blocks = new NativeArray<short>(blocks, Allocator.Persistent),
            size = ChunkManager.chunkSize,
            cubeSize = ChunkManager.cubeSize,
            classes = chunkClasses._classes,
            verts = new NativeArray<float3>(numberOfVerts, Allocator.Persistent),
            normals = new NativeArray<float3>(numberOfVerts, Allocator.Persistent),
            uv = new NativeArray<float2>(numberOfVerts, Allocator.Persistent),
            tris = new NativeArray<int2>(numberOfTris, Allocator.Persistent)
        };

        JobHandle meshHandle = meshInfo.Schedule<JGenerateMesh>();

        Mesh m = new Mesh();

        meshHandle.Complete();
        
        Vector3[] vertices = new Vector3[meshInfo.verts.Length];
        for(int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = meshInfo.verts[i];
        }
        m.vertices = vertices;
        Vector3[] normals = new Vector3[meshInfo.normals.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = meshInfo.normals[i];
        }
        m.normals = normals;
        Vector2[] uv = new Vector2[meshInfo.uv.Length];
        for (int i = 0; i < uv.Length; i++)
        {
            uv[i] = meshInfo.uv[i];
        }
        m.uv = uv;

        Dictionary<short, int> blockLUT = new Dictionary<short, int>();
        List<int>[] triangles = new List<int>[types.Count];
        m.subMeshCount = types.Count;
        Material[] mats = new Material[types.Count];
        int[] iterray = new int[types.Count];
        int j = 0;
        foreach(short s in types.Keys)
        {
            blockLUT.Add(s, j);
            mats[j] = BlockMaterialManager.mats[s - 1];
            triangles[j] = new List<int>();
            j++;
        }
        for (int i = 0; i < meshInfo.tris.Length; i++)
        {
            int triType = meshInfo.tris[i].y;
            int triPoint = meshInfo.tris[i].x;
            j = blockLUT[(short)triType];
            triangles[j].Add(triPoint);
        }
        for (int i = 0; i < triangles.Length; i++)
        {
            m.SetTriangles(triangles[i].ToArray(), i);
        }

        mr.materials = mats;
        mf.mesh = m;
        mc.sharedMesh = m;

        chunkClasses._blocks.Dispose();
        chunkClasses._classes.Dispose();
        meshInfo.verts.Dispose();
        meshInfo.normals.Dispose();
        meshInfo.uv.Dispose();
        meshInfo.tris.Dispose();
        meshInfo.blocks.Dispose();
        

    }

    private struct JUpdateChunk : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<short> _blocks;
        [ReadOnly]
        public int size;

        public void Execute(int i)
        {
            //X stores data on adjacent blocks, Y on triCount
            byte3 outClass = new byte3 { x = 0, y = 0, z =0 };

            if (_blocks[i] != 0)
            {
                //Left
                if (i % size == 0)
                {
                    outClass.x |= 0b00000001;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i - 1] == 0)
                    {
                        outClass.x |= 0b00000001;
                        outClass.y += 6;
                        outClass.z += 4;
                    }
                }

                //Right
                if (i % size == size - 1)
                {
                    outClass.x |= 0b00000010;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i + 1] == 0)
                    {
                        outClass.x |= 0b00000010;
                        outClass.y += 6;
                        outClass.z += 4;
                    }
                }

                //Below
                if (i % (size * size) < size)
                {
                    outClass.x |= 0b00000100;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i - size] == 0)
                    {
                        outClass.x |= 0b00000100;
                        outClass.y += 6;
                        outClass.z += 4;
                    }
                }

                //Above
                if (i % (size * size) >= size * size - size)
                {
                    outClass.x |= 0b00001000;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i + size] == 0)
                    {
                        outClass.x |= 0b00001000;
                        outClass.y += 6;
                        outClass.z += 4;
                    }
                }

                //Back
                if (i < size * size)
                {
                    outClass.x |= 0b00010000;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i - size * size] == 0)
                    {
                        outClass.x |= 0b00010000;
                        outClass.y += 6;
                        outClass.z += 4;

                    }
                }

                //Front
                if (i >= size * size * size - size * size)
                {
                    outClass.x |= 0b00100000;
                    outClass.y += 6;
                    outClass.z += 4;
                }
                else
                {
                    if (_blocks[i + size * size] == 0)
                    {
                        outClass.x |= 0b00100000;
                        outClass.y += 6;
                        outClass.z += 4;

                    }
                }

            }

            _classes[i] = outClass;
        }

        public NativeArray<byte3> _classes;
    }

    private struct JGenerateMesh : IJob
    {
        [ReadOnly]
        public NativeArray<byte3> classes;
        [ReadOnly]
        public NativeArray<short> blocks;
        [ReadOnly]
        public int size;
        [ReadOnly]
        public float cubeSize;

        public NativeArray<float3> verts;
        public NativeArray<float3> normals;
        public NativeArray<float2> uv;
        public NativeArray<int2> tris;

        public void Execute()
        {
            int numOfTris = 0;
            int numOfVerts = 0;

            for (int i = 0; i < classes.Length; i++)
            {
                float3 blockCoord = new float3
                {
                    x = (cubeSize) * (i % size),
                    y = (cubeSize) * (Mathf.FloorToInt(i / size) % size),
                    z = (cubeSize) * Mathf.FloorToInt(i / (size * size))
                };

                if ((classes[i].x & 0b00000001) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = -1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = -1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = -1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = -1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i] };
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                }

                if ((classes[i].x & 0b00000010) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 1,
                        y = 0,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i] };
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i] };
                    numOfTris++;
                }

                if ((classes[i].x & 0b00000100) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = -1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = -1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = -1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = -1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                }

                if ((classes[i].x & 0b00001000) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 1,
                        z = 0
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                }

                if ((classes[i].x & 0b00010000) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = -1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = -1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = -1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 0 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = -1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                }

                if ((classes[i].x & 0b00100000) != 0)
                {
                    //FIRST
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = 1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 0
                    };
                    numOfVerts += 1;

                    //SECOND
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 0 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = 1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 0
                    };
                    numOfVerts += 1;

                    //THIRD
                    verts[numOfVerts] = new float3
                    {
                        x = 1 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = 1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 0,
                        y = 1
                    };
                    numOfVerts += 1;

                    //FOURTH
                    verts[numOfVerts] = new float3
                    {
                        x = 0 * cubeSize + blockCoord.x,
                        y = 1 * cubeSize + blockCoord.y,
                        z = 1 * cubeSize + blockCoord.z
                    };
                    normals[numOfVerts] = new float3
                    {
                        x = 0,
                        y = 0,
                        z = 1
                    };
                    uv[numOfVerts] = new float2
                    {
                        x = 1,
                        y = 1
                    };
                    numOfVerts += 1;

                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 3, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 4, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 2, y = blocks[i]};;
                    numOfTris++;
                    tris[numOfTris] = new int2 { x = numOfVerts - 1, y = blocks[i]};;
                    numOfTris++;
                }

            }
        }
    }


    private struct byte3
    {
        public byte x;
        public byte y;
        public byte z;
    }
}
