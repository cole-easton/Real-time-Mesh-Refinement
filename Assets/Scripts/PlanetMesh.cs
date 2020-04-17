using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(MeshFilter))]
public class PlanetMesh : MonoBehaviour
{

	private Mesh mesh;
	private MeshTree meshTree;
	private List<int> triangles = new List<int>();
	private List<Vector3> vertices = new List<Vector3>();
	private Dictionary<Int64, int> middlePointIndexCache;
	// Start is called before the first frame update
	void Start()
    {
		mesh = new Mesh();
		gameObject.GetComponent<MeshFilter>().mesh = mesh;
		middlePointIndexCache = new Dictionary<long, int>();
		meshTree = BuildIcosphere();
		//meshTree.Refine(t => 3);
		//meshTree.Refine(t => 7 - (int)(Vector3.Angle(t.GetCenter(meshTree.Vertices), Vector3.up)/30 + 0.5));
		//meshTree.Refine(t => 1);
		//meshTree.Refine(t => t.GetCenter(meshTree.Vertices).y > 0 ? 1 : 0);
		//Refine(Vector3.up, 7);
		//AddPerlin(.5f, 5);
		mesh.RecalculateNormals();
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time % 0.1 - Time.deltaTime < 0)
		{
			Refine(new Vector3(Mathf.Cos(Time.time * 6), Mathf.Sin(Time.time * 6), 0), 5);
			mesh.vertices = meshTree.Vertices;
			mesh.triangles = meshTree.LeafTriangles;
			mesh.RecalculateNormals();
		}
    }

	MeshTree BuildIcosphere()
	{
		var phi = (1 + Mathf.Sqrt(5)) / 2;
		addVertex(new Vector3(-1, phi, 0));
		addVertex(new Vector3(1, phi, 0));
		addVertex(new Vector3(-1, -phi, 0));
		addVertex(new Vector3(1, -phi, 0));

		addVertex(new Vector3(0, -1, phi));
		addVertex(new Vector3(0, 1, phi));
		addVertex(new Vector3(0, -1, -phi));
		addVertex(new Vector3(0, 1, -phi));

		addVertex(new Vector3(phi, 0, -1));
		addVertex(new Vector3(phi, 0, 1));
		addVertex(new Vector3(-phi, 0, -1));
		addVertex(new Vector3(-phi, 0, 1));
		triangles = new List<int>()
		{
			0, 11, 5,
			0, 5, 1,
			0, 1, 7,
			0, 7, 10,
			0, 10, 11,
			1, 5, 9,
			5, 11, 4,
			11, 10, 2,
			10, 7, 6,
			7, 1, 8,
			3, 9, 4,
			3, 4, 2,
			3, 2, 6,
			3, 6, 8,
			3, 8, 9,
			4, 9, 5,
			2, 4, 11,
			6, 2, 10, 
			8, 6, 7,
			9, 8, 1
		};
		return new MeshTree(vertices, triangles);
	}
	
	void Refine(Vector3 targetDir, int maxDegree)
	{
		float signedVol(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			return Vector3.Dot(Vector3.Cross(b - a, c - a), d - a);
		}
		List<int> newTris = new List<int>(triangles.Count * 2);
		
		Vector3 q0 = targetDir * -1000, q1 = targetDir * 1000;
		meshTree.Refine(delegate (Triangle t)
		{
			float v0 = signedVol(q0, meshTree.Vertices[t.Vertex0], meshTree.Vertices[t.Vertex1], meshTree.Vertices[t.Vertex2]);
			float v1 = signedVol(q1, meshTree.Vertices[t.Vertex0], meshTree.Vertices[t.Vertex1], meshTree.Vertices[t.Vertex2]);
			float v2 = signedVol(q0, q1, meshTree.Vertices[t.Vertex0], meshTree.Vertices[t.Vertex1]);
			float v3 = signedVol(q0, q1,  meshTree.Vertices[t.Vertex1], meshTree.Vertices[t.Vertex2]);
			float v4 = signedVol(q0, q1, meshTree.Vertices[t.Vertex2], meshTree.Vertices[t.Vertex0]);
			if (Vector3.Dot(targetDir, t.GetCenter(meshTree.Vertices)) > 0 &&
				(v0 > 0 ^ v1 > 0) && v2 > 0 == v3 > 0 && v3 > 0 == v4 > 0 && v4 > 0 == v2 > 0)
			{
				return maxDegree;
			}
			else
			{
				return (int)(maxDegree - Math.Pow(Vector3.Angle(targetDir, t.GetCenter(meshTree.Vertices))/180f, 0.5)*maxDegree + 0.5);
			}
		});
		mesh.vertices = meshTree.Vertices;
		mesh.triangles = meshTree.LeafTriangles;
		mesh.RecalculateNormals();
	}

	private void addVertex(Vector3 p, bool normalize = true)
	{
		float length = normalize?Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z):1f;
		vertices.Add(new Vector3(p.x / length, p.y / length, p.z / length));
	}

	void AddPerlin(float baseMagnitude, int numOctaves, float initialLOD = 1, float decayRatio = 2)
	{
		for (int i = 0; i < vertices.Count; i++)
		{
			float sample = 1;
			for (int o = 0; o < numOctaves; o++)
			{
				sample += baseMagnitude * Mathf.Pow(decayRatio, -o) * PerlinNoise.Noise(
					vertices[i].x * initialLOD * Mathf.Pow(decayRatio, o),
					vertices[i].y * initialLOD * Mathf.Pow(decayRatio, o),
					vertices[i].z * initialLOD * Mathf.Pow(decayRatio, o));
			}
			vertices[i] = vertices[i] * (sample+1);
			mesh.vertices = vertices.ToArray();
		}
		
	}
}
