using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshTree
{
	private List<Vector3> vertices;
	public Vector3[] Vertices
	{
		get
		{
			return vertices.ToArray();
		}
	}
	public Mesh Mesh
	{
		get
		{
			Mesh mesh = new Mesh();
			mesh.vertices = Vertices;
			mesh.triangles = LeafTriangles;
			return mesh;
		}
	}

	private Node[] roots;
	private LinkedList<Node> leafNodes;
	private LinkedList<Node> superleafNodes; //one level up from leaves
	private Dictionary<Int64, int> middlePointIndexCache;
	private Dictionary<int, LinkedList<Node>> nodeByMidpointCache;

	public MeshTree(IList<Vector3> vertices, IList<int> triangles)
	{
		roots = new Node[triangles.Count/3];
		leafNodes = new LinkedList<Node>();
		superleafNodes = new LinkedList<Node>();
		middlePointIndexCache = new Dictionary<long, int>();
		nodeByMidpointCache = new Dictionary<int, LinkedList<Node>>();
		this.vertices = new List<Vector3>(vertices);
		for (int i = 0; i < triangles.Count; i+=3)
		{
			Node n = new Node(new Triangle(triangles[i], triangles[i + 1], triangles[i + 2]));
			leafNodes.AddFirst(n.LinkedListNode);
			roots[i/3] = n;
		}
	}
	public int[] LeafTriangles
	{
		get
		{
			int[] tris = new int[leafNodes.Count * 3];
			int index = 0;
			foreach(Node leafNode in leafNodes)
			{
				tris[index] = leafNode.Triangle.Vertex0;
				tris[index + 1] = leafNode.Triangle.Vertex1;
				tris[index + 2] = leafNode.Triangle.Vertex2;
				index += 3;
			}
			return tris;
		}
	}

	public void MinifyVertices()
	{
		int[] indexHash = new int[vertices.Count]; //new index + 1 at index old index of vertex
		int[] triangles = LeafTriangles;
		List<Vector3> minifiedVerts = new List<Vector3>();
		int vertexIndex = 0;
		for (int i = 0; i < triangles.Length; i++)
		{
			if (indexHash[triangles[i]] == 0)
			{
				minifiedVerts.Add(vertices[triangles[i]]);
				indexHash[triangles[i]] = ++vertexIndex;
			}
		}
		vertices = minifiedVerts;
		foreach (Node root in roots)
		{
			replaceIndices(root, indexHash);
		}
		Dictionary<long, int> newIndexCache = new Dictionary<long, int>();
		foreach(long key in middlePointIndexCache.Keys)
		{
			int value = middlePointIndexCache[key];
			if (value >= indexHash.Length || value < 0)
			{
				continue;
			}
			int larger = unchecked((int)key);
			int smaller = (int)(key >> 32);
			if (indexHash[smaller] == 0 || indexHash[larger] == 0)
			{
				//a hash remains in the table for vertices not in the structure; ideally, this would not occur, but this is a workaround
				continue;
			}
			long newKey = getHash(indexHash[smaller]-1, indexHash[larger]-1);
			newIndexCache[newKey] = indexHash[value]-1;
		}
		middlePointIndexCache = newIndexCache;
		Dictionary<int, LinkedList<Node>> newNodeCache = new Dictionary<int, LinkedList<Node>>();
		foreach (int key in nodeByMidpointCache.Keys)
		{
			if (key > indexHash.Length || indexHash[key] == 0)
			{
				continue;
			}
			newNodeCache[indexHash[key]-1] = nodeByMidpointCache[key];
		}
		nodeByMidpointCache = newNodeCache;

	}

	private void replaceIndices(Node head, int[] replacementTable)
	{
		Debug.Assert(head.Children == null == leafNodes.Contains(head));
		Triangle tri = head.Triangle;
		tri = new Triangle(replacementTable[tri.Vertex0] - 1, replacementTable[tri.Vertex1] - 1, replacementTable[tri.Vertex2] - 1);
		head.Triangle = tri;
		if (head.Children != null)
		{
			foreach(Node child in head.Children)
			{
				replaceIndices(child, replacementTable);
			}
		}
	}

	/// <summary>
	/// Removes all decendants of Node n so that n.Trangle becomes part of the surface geometry of the mesh
	/// </summary>
	/// <param name="n">The node to reduce</param>
	/// <returns>Whether the node was successfully reduced</returns>
	private bool reduceNode(Node n)
	{
		if (n.Children == null)
		{
			return false;
		}
		if (!n.IsSuperleaf()) //is this to expensive, should we only do it if we know we're reducing a dependant?
		{
			foreach(Node child in n.Children)
			{
				reduceNode(child);
			}
		}
		int i0 = n.Triangle.Vertex0;
		int i1 = n.Triangle.Vertex1;
		int i2 = n.Triangle.Vertex2;
		int mid0, mid1, mid2;
		if (!middlePointIndexCache.TryGetValue(getHash(n.Triangle.Vertex0, n.Triangle.Vertex1), out mid0)) { mid0 = -1; }
		if (!middlePointIndexCache.TryGetValue(getHash(n.Triangle.Vertex1, n.Triangle.Vertex2), out mid1)) { mid1 = -1; }
		if (!middlePointIndexCache.TryGetValue(getHash(n.Triangle.Vertex0, n.Triangle.Vertex2), out mid2)) { mid2 = -1; }
		if (nodeByMidpointCache.ContainsKey(mid0)) //if we're reducing a split node it'll only have one midpoint in the dict
			nodeByMidpointCache[mid0].Remove(n);
		if (nodeByMidpointCache.ContainsKey(mid1))
			nodeByMidpointCache[mid1].Remove(n);
		if (nodeByMidpointCache.ContainsKey(mid2))
			nodeByMidpointCache[mid2].Remove(n);
		foreach (Node child in n.Children)
		{
			try
			{
				leafNodes.Remove(child.LinkedListNode);
			}
			catch
			{
				Debug.Log("Children.count: " + n.Children.Length);
			}
		}
		n.Children = null;
		superleafNodes.Remove(n.LinkedListNode);
		leafNodes.AddFirst(n.LinkedListNode);
		if (n.Parent != null && n.Parent.IsSuperleaf())
		{
			superleafNodes.AddFirst(n.Parent.LinkedListNode);
		}
		if (n.Dependents != null)
		{
			foreach (Node dependent in n.Dependents)
			{
				reduceNode(dependent);
			}
		}
		n.Dependents = null;
		return true;

	}

	/// <summary>
	/// Splits the geometry of n.Triangle into 4 subtrangles, unless n has only one sibling, in which case it's parent will be reconstructed to have 4 children
	/// </summary>
	/// <param name="n">the node to refine</param>
	/// <returns>Whether subgeometry was added to n.Triangle itself</returns>
	private bool refineNode(Node n)
	{
		//this is expected to happen on the second sibling of an odd-depth pair, which should be handled externally, but robustness is important
		if (n.LinkedListNode.List != leafNodes ||  n.Children != null) 
		{
			return false;
		}
		if (n.Depth % 2 == 1)
		{
			reduceNode(n.Parent);
			refineNode(n.Parent);
			return false;
		}
		Node[] children = new Node[4];
		int i0 = n.Triangle.Vertex0;
		int i1 = n.Triangle.Vertex1;
		int i2 = n.Triangle.Vertex2;
		int mid0 = getMiddlePoint(i0, i1);
		int mid1 = getMiddlePoint(i1, i2);
		int mid2 = getMiddlePoint(i2, i0);
		if (!nodeByMidpointCache.ContainsKey(mid0))
		{
			nodeByMidpointCache[mid0] = new LinkedList<Node>();
		}
		if (!nodeByMidpointCache.ContainsKey(mid1))
		{
			nodeByMidpointCache[mid1] = new LinkedList<Node>();
		}
		if (!nodeByMidpointCache.ContainsKey(mid2))
		{
			nodeByMidpointCache[mid2] = new LinkedList<Node>();
		}
		nodeByMidpointCache[mid0].AddFirst(n);
		nodeByMidpointCache[mid1].AddFirst(n);
		nodeByMidpointCache[mid2].AddFirst(n);
		if (n.Parent != null && n.Parent.IsSuperleaf())
			superleafNodes.Remove(n.Parent.LinkedListNode);
		children[0] = new Node(new Triangle(i0, mid0, mid2));
		children[1] = new Node(new Triangle(i1, mid1, mid0));
		children[2] = new Node(new Triangle(i2, mid2, mid1));
		children[3] = new Node(new Triangle(mid2, mid0, mid1));
		n.Children = children;
		foreach(Node child in children)
		{
			leafNodes.AddFirst(child.LinkedListNode);
		}
		leafNodes.Remove(n.LinkedListNode);
		superleafNodes.AddFirst(n.LinkedListNode);
		return true;
	}

	private void cleanup(bool allowDoubleSplitting = false)
	{
		bool keepProcessing;
		do
		{
			keepProcessing = false;
			Queue<Node> splitQueue = new Queue<Node>(); //Queue of faces to split in half
			Queue<Node> refineQueue = new Queue<Node>();
			foreach (Node node in leafNodes)
			{
				int[] mids = new int[3];
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex1), out mids[0])) { mids[0] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex1, node.Triangle.Vertex2), out mids[1])) { mids[1] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex2), out mids[2])) { mids[2] = -1; }
				for (int i = 0; i < mids.Length; i++)
				{
					if (mids[i] != -1 && nodeByMidpointCache.ContainsKey(mids[i]) && (nodeByMidpointCache[mids[i]] == null || nodeByMidpointCache[mids[i]].Count == 0))
					{
						middlePointIndexCache.Remove(getHash(node.Triangle.GetVertexByIndex(i), node.Triangle.GetVertexByIndex((i + 1) % 3)));
						mids[i] = -1;
					}
				}
				int numMidpoints = (mids[0] != -1 ? 1 : 0) + (mids[1] != -1 ? 1 : 0) + (mids[2] != -1 ? 1 : 0);
				switch (numMidpoints)
				{
					case 0:
						break; //we're not bordering any more-highly-refined areas; do nothing
					case 1:
						keepProcessing = true;
						if (allowDoubleSplitting || node.Depth % 2 == 0)
							splitQueue.Enqueue(node);
						else
							refineQueue.Enqueue(node);
						break;
					default:
						keepProcessing = true;
						refineQueue.Enqueue(node);
						break;
				}
			}
			// at this point we've removed all midpoints indices from the cache that are not used by any leaf nodes
			while (splitQueue.Count > 0)
			{
				Node node = splitQueue.Dequeue();
				int[] mids = new int[3];
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex1), out mids[0])) { mids[0] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex1, node.Triangle.Vertex2), out mids[1])) { mids[1] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex2), out mids[2])) { mids[2] = -1; }
				Debug.Assert((mids[0] != -1 ? 1 : 0) + (mids[1] != -1 ? 1 : 0) + (mids[2] != -1 ? 1 : 0) == 1);
				for (int j = 0; j < 3; j++)
				{
					if (mids[j] != -1)
					{
						Node[] children = new Node[2];
						children[0] = new Node(new Triangle(mids[j], node.Triangle.GetVertexByIndex((j + 2) % 3), node.Triangle.GetVertexByIndex(j)));
						children[1] = new Node(new Triangle(mids[j], node.Triangle.GetVertexByIndex((j + 1) % 3), node.Triangle.GetVertexByIndex((j + 2) % 3)));
						if (node.Parent != null && node.Parent.IsSuperleaf())
							superleafNodes.Remove(node.Parent.LinkedListNode);
						node.Children = children;
						leafNodes.AddFirst(children[0].LinkedListNode);
						leafNodes.AddFirst(children[1].LinkedListNode);
						leafNodes.Remove(node.LinkedListNode);
						superleafNodes.AddFirst(node.LinkedListNode);
						Node inverseDependent = nodeByMidpointCache[mids[j]].First.Value;
						if (inverseDependent.Dependents == null)
							inverseDependent.Dependents = new List<Node>(3);
						inverseDependent.Dependents.Add(node);

						/* while we may not need this (as it's unlikely there'd be a flat node next to a split one, since splitting only happens if the adjacent
						 * node is refined) there may be some cases I'm not considering, and this node does have the given midpoint, so it's sensible to add it */
						nodeByMidpointCache[mids[j]].AddFirst(node);
						break;
					}
				}
			}
			while (refineQueue.Count > 0)
			{
				refineNode(refineQueue.Dequeue());

			}
		} while (keepProcessing);
	}

	public void Refine(Func<Triangle, int> getRefinementDegree)
	{
		int loopCount = 0;
		bool keepProcessing;
		do
		{
			keepProcessing = false;
			Queue<Node> processQueue = new Queue<Node>();
			foreach (Node node in superleafNodes)
			{
				if (node.Depth + 2 > getRefinementDegree(node.Triangle) * 2)
				{
					processQueue.Enqueue(node);
					keepProcessing = true;
				}
			}
			while (processQueue.Count > 0)
			{
				reduceNode(processQueue.Dequeue());
			}
		} while (keepProcessing && ++loopCount < 20);
		loopCount = 0;
		do
		{
			keepProcessing = false;
			//nodeByMidpointCache.Clear();
			Queue<Node> processQueue = new Queue<Node>();
			foreach (Node node in leafNodes)
			{
				if (node.Depth < getRefinementDegree(node.Triangle) * 2)
				{
					processQueue.Enqueue(node);
					keepProcessing = true;
				}
			}
			while (processQueue.Count > 0)
			{
				refineNode(processQueue.Dequeue());
			}
			cleanup();
		} while (keepProcessing && ++loopCount < 20);
		Debug.Log("leaves: " + leafNodes.Count + "\nSuperleaves: " + superleafNodes.Count);
	}

	private void addVertex(Vector3 p, bool normalize = true)
	{
		float length = normalize ? Mathf.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z) : 1f;
		vertices.Add(new Vector3(p.x / length, p.y / length, p.z / length));
	}

	private Int64 getHash(int a, int b)
	{
		bool firstIsSmaller = a < b;
		Int64 smallerIndex = firstIsSmaller ? a : b;
		Int64 greaterIndex = firstIsSmaller ? b : a;
		if (smallerIndex < 0)
		{
			Debug.Log("ALERT, INVALID INDEX");
			Debug.Log(System.Environment.StackTrace);
		}
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
}

class Node
{
	private Node[] children;

	public Node(Triangle triangle)
	{
		Triangle = triangle;
		Depth = 0;
		children = null;
		Parent = null;
		Dependents = null;
		LinkedListNode = new LinkedListNode<Node>(this);
	}
	public Triangle Triangle { get; internal set; }
	public Node[] Children
	{
		get
		{
			return children;
		}
		set
		{
			children = value;
			if (children == null)
			{
				return;
			}
			foreach (Node child in children)
			{
				child.Parent = this;
				child.Depth = this.Depth + children.Length / 2;
			}
		}
	}
	public bool IsSuperleaf()
	{
		if (children == null) return false; //regular leaf
		foreach(Node child in children)
		{
			if (child.children != null) return false; //has superleaf child -> not superleaf
		}
		return true;
	}
	public int Depth { get; private set; }
	public List<Node> Dependents { get; set; }

	public Node Parent { get; private set; }

	public LinkedListNode<Node> LinkedListNode { get; }

}

public struct Triangle
{
	public Triangle(int vertex0, int vertex1, int vertex2)
	{
		Vertex0 = vertex0;
		Vertex1 = vertex1;
		Vertex2 = vertex2;
	}
	public int Vertex0 { get; }
	public int Vertex1 { get; }
	public int Vertex2 { get; }
	public Vector3 GetCenter(IList<Vector3> vertices)
	{
		return (vertices[Vertex1] + vertices[Vertex1] + vertices[Vertex2]) / 3f;
	}
	public int GetVertexByIndex(int index)
		{
			switch (index)
			{
				case 0:
					return Vertex0;;
				case 1:
					return Vertex1;
				case 2:
					return Vertex2;
			}
			return -1;
		}
}
