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
		meshTree.Refine(t => 3);
		//meshTree.Refine(t => 10 - (int)(Vector3.Angle(t.GetCenter(meshTree.Vertices), Vector3.up)/20 + 0.5));
		mesh.vertices = meshTree.Vertices;
		mesh.triangles = meshTree.LeafTriangles;

		//for (int i = 0; i < 2; i++)
		//{
		//	Refine(Vector3.up, 180);
		//}
		//for (int i = 0; i < 2; i++)
		//{
		//	Refine(Vector3.up, 90);
		//}
		//for (int i = 0; i < 2; i++)
		//{
		//	Refine(Vector3.up, 30);
		//}
		//AddPerlin(.5f, 5);
		mesh.RecalculateNormals();
    }

    // Update is called once per frame
    void Update()
    {
        
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
	
	void Refine(Vector3 targetDir, float angle)
	{
		float signedVol(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
		{
			return Vector3.Dot(Vector3.Cross(b - a, c - a), d - a);
		}
		List<int> newTris = new List<int>(triangles.Count * 2);
		
		Vector3 q0 = targetDir * -1000, q1 = targetDir * 1000;
		for (int i = 0; i < triangles.Count; i+=3)
		{
			float v0 = signedVol(q0, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
			float v1 = signedVol(q1, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
			float v2 = signedVol(q0, q1, vertices[triangles[i]], vertices[triangles[i + 1]]);
			float v3 = signedVol(q0, q1, vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
			float v4 = signedVol(q0, q1, vertices[triangles[i + 2]], vertices[triangles[i]]);
			Vector3 center = (vertices[triangles[i]] + vertices[triangles[i+1]] + vertices[triangles[i+2]]) / 3f;
			if (Vector3.Angle(targetDir, center) < angle || Vector3.Dot(targetDir, vertices[triangles[i]]) > 0 &&
				(v0>0^v1>0)&&v2>0==v3>0&&v3>0==v4>0&&v4>0==v2>0)
			{
				int i0 = triangles[i];
				int i1 = triangles[i + 1];
				int i2 = triangles[i + 2];
				int mid0 = getMiddlePoint(i0, i1);
				int mid1 = getMiddlePoint(i1, i2);
				int mid2 = getMiddlePoint(i2, i0);
				newTris.AddRange(new int[]{
					i0, mid0, mid2,
					i1, mid1, mid0,
					i2, mid2, mid1,
					mid2, mid0, mid1
				});
			}
			else
			{
				newTris.AddRange(triangles.GetRange(i, 3));
			}
		}
		triangles = newTris;
		mesh.vertices = vertices.ToArray();
		mesh.triangles = newTris.ToArray();
		Cleanup();
	}

	/// <summary>
	/// Fixes geometry of mesh so there are no surface gaps or hard lines
	/// </summary>
	/// <returns>Whether anything was modified during the cleanup</returns>
	bool Cleanup()
	{
		bool modified = false;
		List<int> fixedTris = new List<int>(triangles);
		int fixedI = 0; //index in fixedTris
		for (int i = 0; i < triangles.Count; i+=3)
		{
			int[] mids = new int[3];
			// first check if we have it already
			Int64 key = ((Int64)i << 32) + (Int64)(i+1);
			if (!middlePointIndexCache.TryGetValue(getHash(triangles[i], triangles[i + 1]), out mids[0])) { mids[0] = -1; }
			if (!middlePointIndexCache.TryGetValue(getHash(triangles[i+1], triangles[i + 2]), out mids[1])) { mids[1] = -1; }
			if (!middlePointIndexCache.TryGetValue(getHash(triangles[i], triangles[i + 2]), out mids[2])) { mids[2] = -1; }
			switch ((mids[0] != -1 ? 1 : 0) + (mids[1] != -1 ? 1 : 0) + (mids[2] != -1 ? 1 : 0))
			{
				case 0:
					fixedI += 3;
					break; //we're not bordering any more-highly-refined areas; do nothing
				case 1:
					modified = true;
					for (int j = 0; j < 3; j++)
					{
						if (mids[j] != -1)
						{
							fixedTris.AddRange(new int[] {
								mids[j], triangles[i+(j+2)%3], triangles[i+j],
								mids[j], triangles[i+(j+1)%3], triangles[i+(j+2)%3]
							});

						}
					}
					fixedTris.RemoveRange(fixedI, 3);
					break;
				default:
					modified = true;
					int i0 = triangles[i];
					int i1 = triangles[i + 1];
					int i2 = triangles[i + 2];
					int mid0 = getMiddlePoint(i0, i1, true);
					int mid1 = getMiddlePoint(i1, i2, true);
					int mid2 = getMiddlePoint(i2, i0, true);
					fixedTris.AddRange(new int[]{
						i0, mid0, mid2,
						i1, mid1, mid0,
						i2, mid2, mid1,
						mid2, mid0, mid1
					});
					fixedTris.RemoveRange(fixedI, 3);
					mesh.vertices = vertices.ToArray();
					break;
			}

		}
		mesh.triangles = fixedTris.ToArray();
		triangles = fixedTris;
		if (modified)
			Cleanup();
		return modified;
	}

	private void addVertex(Vector3 p, bool normalize = true)
	{
		float length = normalize?Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z):1f;
		vertices.Add(new Vector3(p.x / length, p.y / length, p.z / length));
	}

	private Int64 getHash(int a, int b)
	{
		bool firstIsSmaller = a < b;
		Int64 smallerIndex = firstIsSmaller ? a : b;
		Int64 greaterIndex = firstIsSmaller ? b : a;
		return (smallerIndex << 32) + greaterIndex;
	}

	// return index of point in the middle of p1 and p2
	private int getMiddlePoint(int p1, int p2, bool normalize = true)
	{
		Int64 key = getHash(p1, p2);
		int ret;
		if (this.middlePointIndexCache.TryGetValue(key, out ret))
		{
			return ret;
		}

		// not in cache, calculate it
		Vector3 point1 = vertices[p1];
		Vector3 point2 = vertices[p2];
		Vector3 middle = (point1 + point2) / 2.0f;

		// add vertex makes sure point is on unit sphere
		addVertex(middle, normalize);
		int i = vertices.Count - 1;

		// store it, return index
		this.middlePointIndexCache.Add(key, i);
		return i;
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
