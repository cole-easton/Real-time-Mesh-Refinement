using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(MeshFilter))]
public class PlanetMesh : MonoBehaviour
{

	private MeshFilter meshFilter;
	private MeshTree meshTree;
	private List<int> triangles = new List<int>();
	private List<Vector3> vertices = new List<Vector3>();
	private Dictionary<Int64, int> middlePointIndexCache;
	private float perlinMag = 0.2f;
	private int perlinOctaves = 10;
	private int perlinInitLOD = 1;
	private float perlinDecay = 2;
	// Start is called before the first frame update
	void Start()
    {
		meshFilter = gameObject.GetComponent<MeshFilter>();
		middlePointIndexCache = new Dictionary<long, int>();
		meshTree = BuildIcosphere();
		meshTree.MinifyVertices();
		meshFilter.mesh = meshTree.Mesh;
		Bounds bounds = meshFilter.mesh.bounds;
		bounds.extents = new Vector3((float)(transform.localScale.x * (1.01 + perlinMag)), (float)(transform.localScale.x * (1.01 + perlinMag)), (float)(transform.localScale.x * (1.01 + perlinMag)));
		meshFilter.mesh.bounds = bounds;
		meshFilter.mesh.RecalculateNormals();
	}

    // Update is called once per frame
    void Update()
    {

    }

	public void UpdateRefinement(Vector3 pos, int maxDegree = 7)
	{
		Refine(pos, maxDegree);
		meshTree.MinifyVertices();
		meshFilter.mesh = meshTree.Mesh;
		AddPerlin();
		meshFilter.mesh.RecalculateNormals();

	}
	public float GetRadius(Vector3 pos)
	{
		float sample = 1;
		pos.Normalize();
		for (int o = 0; o < perlinOctaves; o++)
		{
			sample += perlinMag * Mathf.Pow(perlinDecay, -o) * PerlinNoise.Noise(
				pos.x * perlinInitLOD * Mathf.Pow(perlinDecay, o),
				pos.y * perlinInitLOD * Mathf.Pow(perlinInitLOD, o),
				pos.z * perlinInitLOD * Mathf.Pow(perlinDecay, o));
		}
		return (sample+1) * transform.localScale.x;
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
		
		Vector3 q0 = targetDir * -100, q1 = targetDir * 100;
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
				return (int)(-Math.Log(Vector3.Angle(targetDir, t.GetCenter(meshTree.Vertices))/180f, 2) + 0.5);
			}
		});
	}

	private void addVertex(Vector3 p, bool normalize = true)
	{
		float length = normalize?Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z):1f;
		vertices.Add(new Vector3(p.x / length, p.y / length, p.z / length));
	}

	void AddPerlin(float initialLOD = 1, float decayRatio = 2)
	{
		Vector3[] vertices = meshFilter.mesh.vertices;
		for (int i = 0; i < meshFilter.mesh.vertices.Length; i++)
		{
			float sample = 1;
			for (int o = 0; o < perlinOctaves; o++)
			{
				sample += perlinMag * Mathf.Pow(decayRatio, -o) * PerlinNoise.Noise(
					vertices[i].normalized.x * initialLOD * Mathf.Pow(decayRatio, o),
					vertices[i].normalized.y * initialLOD * Mathf.Pow(decayRatio, o),
					vertices[i].normalized.z * initialLOD * Mathf.Pow(decayRatio, o));
			}
			vertices[i] = vertices[i] * (sample+1);
		}
		meshFilter.mesh.vertices = vertices;
		
	}
}
