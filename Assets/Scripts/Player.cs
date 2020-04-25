using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Player : MonoBehaviour
{
	public PlanetMesh planet;
	public float gravity = 1f;
	public float MoveSpeed = 1;
	public float LookSpeed = 30;
	public float JumpForce = 20;
	private Vector3 velocity;
	// Start is called before the first frame update
	void Start()
    {
		Vector3 up = transform.position - planet.transform.position;
		transform.up = up;
		planet.UpdateRefinement(up, 7);
	}

    // Update is called once per frame
    void Update()
    {
		Vector3 up = transform.position - planet.transform.position;
		gameObject.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(transform.right*90)*transform.up);
		velocity -= gravity*up.normalized * Time.deltaTime;
		transform.position += velocity;
		float radius = planet.GetRadius(transform.position) + 5;
		if (up.sqrMagnitude < radius*radius)
		{
			transform.position = transform.position.normalized * radius;
			velocity = Vector3.zero;
		}
		if (Input.GetKey(KeyCode.UpArrow))
		{
			Vector3 pos = transform.localPosition;
			pos.z += MoveSpeed * Time.deltaTime;
			transform.localPosition = pos;
		}
		else if (Input.GetKey(KeyCode.DownArrow))
		{
			Vector3 pos = transform.localPosition;
			pos.z -= MoveSpeed * Time.deltaTime;
			transform.localPosition = pos;
		}
		if (Input.GetKey(KeyCode.LeftArrow))
		{
			transform.localRotation *= Quaternion.Euler(-Vector3.up * LookSpeed * Time.deltaTime);
		}
		else if (Input.GetKey(KeyCode.RightArrow))
		{
			transform.localRotation *= Quaternion.Euler(Vector3.up * LookSpeed * Time.deltaTime);
		}
		if (Input.GetKeyDown(KeyCode.Space))
		{
			velocity = up.normalized * JumpForce;
		}
	}
}
