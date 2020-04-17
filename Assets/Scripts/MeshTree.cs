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
	private LinkedList<Node> leafNodes;
	private LinkedList<Node> superleafNodes; //one level up from leaves
	private Dictionary<Int64, int> middlePointIndexCache;
	private Dictionary<int, Node> nodeByMidpointCache;

	public MeshTree(IList<Vector3> vertices, IList<int> triangles)
	{
		leafNodes = new LinkedList<Node>();
		superleafNodes = new LinkedList<Node>();
		middlePointIndexCache = new Dictionary<long, int>();
		nodeByMidpointCache = new Dictionary<int, Node>();
		this.vertices = new List<Vector3>(vertices);
		for (int i = 0; i < triangles.Count; i+=3)
		{
			Node n = new Node(new Triangle(triangles[i], triangles[i + 1], triangles[i + 2]));
			leafNodes.AddFirst(n.LinkedListNode);
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

	private bool reduceNode(Node n)
	{
		if (n.Children == null)
			return false;
		foreach (Node child in n.Children)
		{
			leafNodes.Remove(child.LinkedListNode);
		}
		n.Children = null;
		if (n.Dependents != null)
		{
			foreach (Node dependent in n.Dependents)
			{
				reduceNode(dependent);
			}
		}
		n.Dependents = null;
		superleafNodes.Remove(n.LinkedListNode);
		leafNodes.AddFirst(n.LinkedListNode);
		if (n.Parent.IsSuperleaf())
		{
			superleafNodes.AddFirst(n.Parent.LinkedListNode);
		}
		return true;

	}

	private void refineNode(Node n)
	{
		if (n.Depth % 2 == 1)
		{
			reduceNode(n.Parent);
			refineNode(n.Parent);
			return;
		}
		Node[] children = new Node[4];
		int i0 = n.Triangle.Vertex0;
		int i1 = n.Triangle.Vertex1;
		int i2 = n.Triangle.Vertex2;
		int mid0 = getMiddlePoint(i0, i1);
		int mid1 = getMiddlePoint(i1, i2);
		int mid2 = getMiddlePoint(i2, i0);
		nodeByMidpointCache[mid0] = n;
		nodeByMidpointCache[mid1] = n;
		nodeByMidpointCache[mid2] = n;
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
	}

	private void cleanup()
	{
		bool keepProcessing;
		do
		{
			keepProcessing = false;
			Queue<KeyValuePair<bool, Node>> processQueue = new Queue<KeyValuePair<bool, Node>>();
			foreach (Node node in leafNodes)
			{
				int[] mids = new int[3];
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex0), out mids[0])) { mids[0] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex1, node.Triangle.Vertex2), out mids[1])) { mids[1] = -1; }
				if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex2), out mids[2])) { mids[2] = -1; }
				int numMidpoints = (mids[0] != -1 ? 1 : 0) + (mids[1] != -1 ? 1 : 0) + (mids[2] != -1 ? 1 : 0);
				switch (numMidpoints)
				{
					case 0:
						break; //we're not bordering any more-highly-refined areas; do nothing
					case 1:
						keepProcessing = true;
						processQueue.Enqueue(new KeyValuePair<bool, Node>(false, node));
						
						break;
					default:
						processQueue.Enqueue(new KeyValuePair<bool, Node>(true, node));
						break;
				}
			}
			while (processQueue.Count > 0)
			{
				KeyValuePair<bool, Node> pair = processQueue.Dequeue();
				Node node = pair.Value;
				if (pair.Key) //they key represents whether there were multiple adjacencies 
				{
					refineNode(node);
				}
				else
				{
					int[] mids = new int[3];
					if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex0), out mids[0])) { mids[0] = -1; }
					if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex1, node.Triangle.Vertex2), out mids[1])) { mids[1] = -1; }
					if (!middlePointIndexCache.TryGetValue(getHash(node.Triangle.Vertex0, node.Triangle.Vertex2), out mids[2])) { mids[2] = -1; }
					for (int j = 0; j < 3; j++)
					{
						if (mids[j] != -1)
						{
							Node[] children = new Node[2];
							children[0] = new Node(new Triangle(mids[j], node.Triangle.GetVertexByIndex((j + 2) % 3), node.Triangle.GetVertexByIndex(j)));
							children[1] = new Node(new Triangle(mids[j], node.Triangle.GetVertexByIndex((j + 1) % 3), node.Triangle.GetVertexByIndex((j + 2) % 3)));
							if (node.Parent.IsSuperleaf())
								superleafNodes.Remove(node.Parent.LinkedListNode);
							node.Children = children;
							leafNodes.AddFirst(children[0].LinkedListNode);
							leafNodes.AddFirst(children[1].LinkedListNode);
							leafNodes.Remove(node.LinkedListNode);
							superleafNodes.AddFirst(node.LinkedListNode);
							Node inverseDependent = nodeByMidpointCache[mids[j]];
							if (inverseDependent.Dependents == null)
								inverseDependent.Dependents = new List<Node>(3);
							inverseDependent.Dependents.Add(node);
							break;
						}
					}
				}
			}
		} while (keepProcessing);
	}

	public void Refine(Func<Triangle, int> getRefinementDegree)
	{
		Debug.Log("refine in");
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
		} while (keepProcessing);
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
		} while (keepProcessing);
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
	public Triangle Triangle { get; }
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
		if (children == null) return false;
		foreach(Node child in children)
		{
			if (child.children != null) return false;
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
