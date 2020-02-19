﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Linq;
using System;

public class testPathFinding : MonoBehaviour
{
	private Transform seeker;
	public Transform target;

	private static int MOVE_STRIAGHT_COST = 10;
	private static int MOVE_DIAGONAL_COST = 20;
	private int CostofPath = 0;

	public bool DebugCorners = true;
	public bool ToggleHeapOptimisation = true;

	public Node calculatedPathStartNode = null;

	// Start is called before the first frame update
	void Start()
	{
		seeker = transform;
	}

	// Update is called once per frame
	void Update()
	{
		if (target != null && Input.GetKeyDown(KeyCode.W))
		{
			CalculatePath(new Vector2Int(Mathf.RoundToInt(seeker.position.x), Mathf.RoundToInt(seeker.position.y)), new Vector2Int(Mathf.RoundToInt(target.position.x), Mathf.RoundToInt(target.position.y)));
		}
	}

	List<Node> GetNeighbours(Node currentNode)
	{
		List<Node> neighbours = new List<Node>();

		for (int x = -1; x < 2; x++)
		{
			for (int y = -1; y < 2; y++)
			{
				if (!World.IsPositionWalkable(new Vector2Int(x + currentNode.pos.x, y + currentNode.pos.y)))
					continue;
				if (x == 0 && y == 0)
					continue;

				Node newNeighbour = new Node(new Vector2Int(x + currentNode.pos.x, y + currentNode.pos.y));

				if (DebugCorners && x != 0 && y != 0)
				{
					bool adjacentWall = false;

					for (int v = -1; v < 2; v++)
					{
						if (v == 0) continue;

						if (!World.IsPositionWalkable(newNeighbour.pos + new Vector2Int(0, v))) adjacentWall = true;
					}

					for (int h = -1; h < 2; h++)
					{
						if (h == 0) continue;

						if (!World.IsPositionWalkable(newNeighbour.pos + new Vector2Int(h, 0))) adjacentWall = true;
					}

					if (!adjacentWall) neighbours.Add(newNeighbour);
				}
				else
				{
					neighbours.Add(newNeighbour);
				}
			}
		}
		return neighbours;
	}

	public int GetDistance(Vector2Int nodeA, Vector2Int nodeB)
	{
		int distX = Mathf.Abs(nodeA.x - nodeB.x);
		int distY = Mathf.Abs(nodeA.y - nodeB.y);

		if (distX > distY)
			return MOVE_DIAGONAL_COST * distY + MOVE_STRIAGHT_COST * (distX - distY);
		return MOVE_DIAGONAL_COST * distX + MOVE_STRIAGHT_COST * (distY - distX);
	}

	public void CalculatePath(Vector2Int startPos, Vector2Int targetPos)
	{
		if (ToggleHeapOptimisation)
			CalculateOptimisedPath(startPos, targetPos); //This used heap optimisation to run faster
		else
		{
			CalculateSlowPath(startPos, targetPos); //This uses Dictionary to look up nodes (Slower)
		}
	}

	public void CalculateSlowPath(Vector2Int startPos, Vector2Int targetPos)
	{
		Stopwatch sw = new Stopwatch();
		sw.Start();

		if (!World.IsPositionWalkable(targetPos) || !World.IsPositionWalkable(startPos))
		{
			calculatedPathStartNode = null;
			print("Target is out of bounds.");
			return;
		}
		Node startNode = new Node(startPos);
		Node targetNode = new Node(targetPos);

		Dictionary<Vector2Int, Node> OPEN = new Dictionary<Vector2Int, Node>();
		Dictionary<Vector2Int, Node> CLOSED = new Dictionary<Vector2Int, Node>();
		Dictionary<Vector2Int, Node> Operations = new Dictionary<Vector2Int, Node>();

		OPEN.Add(startNode.pos, startNode);

		while (OPEN.Count > 0)
		{
			Dictionary<Vector2Int, Node>.Enumerator currNode = OPEN.GetEnumerator();
			currNode.MoveNext();

			Node currentNode = currNode.Current.Value;
			while (currNode.MoveNext())
			{
				if (currNode.Current.Value.fCost < currentNode.fCost || currNode.Current.Value.fCost == currentNode.fCost && currNode.Current.Value.gCost < currentNode.gCost)
				{
					currentNode = currNode.Current.Value;
				}
			}

			OPEN.Remove(currentNode.pos);
			CLOSED.Add(currentNode.pos, currentNode);

			if (currentNode.pos == targetNode.pos)
			{
				sw.Stop();
				CostofPath = currentNode.gCost;
				print(("Unoptimised Path: " + sw.ElapsedMilliseconds + "ms"));
				print("Operations: " + Operations.Count);
				print("Path Cost: " + pathCost);
				RetracePath(startNode, currentNode);
				OPEN.Clear();
				CLOSED.Clear();
				return;
			}

			foreach (Node neighbour in GetNeighbours(currentNode))
			{
				if (CLOSED.ContainsKey(neighbour.pos))
					continue;
				neighbour.gCost = GetDistance(neighbour.pos, startNode.pos);

				int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode.pos, neighbour.pos);
				if (newMovementCostToNeighbour < neighbour.gCost || !Operations.ContainsKey(neighbour.pos))
				{
					neighbour.gCost = newMovementCostToNeighbour;
					neighbour.hCost = GetDistance(neighbour.pos, targetNode.pos);
					neighbour.parent = currentNode;
					OPEN.Add(neighbour.pos, neighbour);
					Operations.Add(neighbour.pos, neighbour);
				}
			}
		}

		print("Didn't hit target.");

		OPEN.Clear();
		CLOSED.Clear();
	}

	public void CalculateOptimisedPath(Vector2Int startPos, Vector2Int targetPos)
	{
		Stopwatch sw = new Stopwatch();
		sw.Start();

		if (!World.IsPositionWalkable(targetPos) || !World.IsPositionWalkable(startPos))
		{
			calculatedPathStartNode = null;
			print("Target is out of bounds.");
			return;
		}
		Node startNode = new Node(startPos);
		Node targetNode = new Node(targetPos);

		Heap<Node> OPEN = new Heap<Node>(World.WorldMaxSize);
		Dictionary<Vector2Int, Node> Operations = new Dictionary<Vector2Int, Node>();
		Dictionary<Vector2Int, Node> CLOSED = new Dictionary<Vector2Int, Node>();

		OPEN.Add(startNode);

		while (OPEN.Count > 0)
		{
			Node currentNode = OPEN.RemoveFirst();

			CLOSED.Add(currentNode.pos, currentNode);

			if (currentNode.pos == targetNode.pos)
			{
				sw.Stop();
				CostofPath = currentNode.gCost;
				print(("Optimised Path: " + sw.ElapsedMilliseconds + "ms"));
				print("Operations: " + Operations.Count);
				print("Path Cost: " + pathCost);
				RetracePath(startNode, currentNode);
				CLOSED.Clear();
				Operations.Clear();
				return;
			}

			foreach (Node neighbour in GetNeighbours(currentNode))
			{
				if (CLOSED.ContainsKey(neighbour.pos))
					continue;
				neighbour.gCost = GetDistance(neighbour.pos, startNode.pos);
				neighbour.hCost = GetDistance(neighbour.pos, targetNode.pos);

				int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode.pos, neighbour.pos);
				if (newMovementCostToNeighbour < neighbour.gCost || !Operations.ContainsKey(neighbour.pos))
				{
					neighbour.gCost = newMovementCostToNeighbour;
					neighbour.parent = currentNode;
					OPEN.Add(neighbour);
					Operations.Add(neighbour.pos, neighbour);
				}
			}
		}

		print("Didn't hit target.");
		CLOSED.Clear();
		Operations.Clear();
	}

	public int pathCost
	{
		get
		{
			return CostofPath;
		}
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.red;

		if (calculatedPathStartNode != null)
		{
			Node currentNode = calculatedPathStartNode;

			while (currentNode != null)
			{
				Vector3 currentPos = new Vector3(currentNode.pos.x, currentNode.pos.y, 0);

				Gizmos.DrawWireCube(currentPos, new Vector3(1.0f, 1.0f));

				if (currentNode.parent != null)
				{
					Gizmos.DrawLine(currentPos, new Vector3(currentNode.parent.pos.x, currentNode.parent.pos.y, 0));
				}

				currentNode = currentNode.parent;
			}
		}
	}

	void RetracePath(Node startNode, Node targetNode)
	{
		Node previousNode = null;
		Node currentNode = targetNode;

		while (currentNode != startNode)
		{
			Vector3 pos = new Vector3(currentNode.pos.x, currentNode.pos.y, 0);

			Node nextNode = currentNode.parent;

			currentNode.parent = previousNode;
			previousNode = currentNode;
			currentNode = nextNode;
		}

		currentNode.parent = previousNode;
		calculatedPathStartNode = currentNode;
	}
}