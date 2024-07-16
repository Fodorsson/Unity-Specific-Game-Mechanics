using System.Collections;
using UnityEngine;

public class PortalWallCreator : MonoBehaviour
{
	private static Vector2[] wallPoints = new Vector2[]
	{
		new Vector2(10f, 10f),
		new Vector2(10f, -10f),
		new Vector2(-10f, -10f),
		new Vector2(-10f, 10f),
		new Vector2(10f, 10f),
	};

	private static Color[] wallColors = new Color[]
	{
		Color.red,
		Color.blue,
		Color.green,
		Color.yellow,
	};

	public static void CreatePortalWalls()
	{

		for (int i = 0; i < wallPoints.Length - 1; i++)
		{
			GameObject newPortalWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
			newPortalWall.name = "Portal Wall";

			newPortalWall.transform.position = new Vector3((wallPoints[i].x + wallPoints[i + 1].x) / 2f, 10f, (wallPoints[i].y + wallPoints[i + 1].y) / 2f);
			newPortalWall.transform.Rotate(Vector3.up * (i + 1) * 90f);
			newPortalWall.transform.localScale = new Vector3(20f, 20f, 20f);

			Material newMaterial = new Material(Shader.Find("Standard"));
			newMaterial.color = wallColors[i];
			newPortalWall.GetComponent<MeshRenderer>().material = newMaterial;
		}

	}

}

public class PortalCollision : MonoBehaviour
{
	bool isColliding = false;

	private void OnTriggerEnter(Collider other)
	{
		if (other.name == "Portal Object" && !isColliding)
		{
			isColliding = true;

			//We have to set the Main Camera's near clipping plane to the bare minimum, so that when we go through the portal, the rendertexture is still rendered on top of everything else.
			Camera.main.nearClipPlane = 0.01f;

			other.gameObject.GetComponent<Portal>().GetPortalWall().GetComponent<MeshCollider>().convex = true;
			other.gameObject.GetComponent<Portal>().GetPortalWall().GetComponent<MeshCollider>().isTrigger = true;

			///

			int index = other.gameObject.GetComponent<Portal>().GetIndex();

			Vector3 positionOffset = Controls_Portal.portals[1 - index].transform.position - Controls_Portal.portals[index].transform.position;
			Vector3 rotationOffset = Controls_Portal.portals[1 - index].transform.eulerAngles + Vector3.up * 180f - Controls_Portal.portals[index].transform.eulerAngles;

			Rigidbody rb = transform.GetComponent<Rigidbody>();
			Vector3 storedVelocity = Vector3.zero;

			if (rb != null)
			{
				storedVelocity = rb.velocity;
				rb.velocity = Vector3.zero;
			}

			Camera.main.GetComponent<Camera_FirstPerson>().ForceRotate(rotationOffset);

			transform.RotateAround(Controls_Portal.portals[index].transform.position, Vector3.up, rotationOffset.y);
			transform.position += positionOffset;

			rb.velocity = Quaternion.Euler(rotationOffset) * (storedVelocity);
			rb.AddForce(Controls_Portal.portals[1 - index].transform.forward * -5f, ForceMode.Impulse);
		}

	}

	private void OnTriggerExit(Collider other)
	{
		if (other.name == "Portal Object")
		{
			//Now we can afford the original value for the near clipping plane
			Camera.main.nearClipPlane = 0.3f;

			other.gameObject.GetComponent<Portal>().GetPortalWall().GetComponent<MeshCollider>().isTrigger = false;
			other.gameObject.GetComponent<Portal>().GetPortalWall().GetComponent<MeshCollider>().convex = false;

			StartCoroutine(WaitAndDisable());
		}
	}

	IEnumerator WaitAndDisable()
	{
		yield return new WaitForSeconds(0.1f);
		isColliding = false;
	}
}

public class Portal : MonoBehaviour
{
	private Camera portalCamera;
	private GameObject portalWall;
	private int index;

	public Camera GetPortalCamera() { return portalCamera; }
	public void SetPortalCamera(Camera newCamera) { portalCamera = newCamera; }

	public GameObject GetPortalWall() { return portalWall; }
	public void SetPortalWall(GameObject newPortalWall) { portalWall = newPortalWall; }

	public int GetIndex() { return index; }
	public void SetIndex(int newValue) { index = newValue; }
}

public class Controls_Portal : MonoBehaviour
{
	public GameObject portalPrefab;
	public Shader cutoutShader;
	public KeyCode keyAttackMain = KeyCode.Mouse0;
	public KeyCode keyAttackAlt = KeyCode.Mouse1;

	public static GameObject[] portals = new GameObject[2];

	public Texture wallSprite;

	private Color[] portalColors = new Color[] {Color.blue, Color.red};

	private void OnDrawGizmos()
	{
		Debug.DrawLine(Vector3.zero, Camera.main.transform.position + Camera.main.transform.forward * 50f);
	}

	private void Start()
	{
		PortalWallCreator.CreatePortalWalls();

		if (wallSprite != null)
		{
			foreach (var wall in FindObjectsOfType(typeof(GameObject)) as GameObject[])
			{
				if (wall.name == "Portal Wall")
					wall.GetComponent<MeshRenderer>().material.mainTexture = wallSprite;
			}
		}

		gameObject.AddComponent<PortalCollision>();
	}

	void Update()
	{

		if (Input.GetKeyDown(keyAttackMain))
			ShootPortal(0);

		if (Input.GetKeyDown(keyAttackAlt))
			ShootPortal(1);

		if (portals[0] != null && portals[1] != null)
		{
			ReorientPortal(0);
			ReorientPortal(1);
		}

	}

	private void ShootPortal(int index)
	{
		RaycastHit hit;
		if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit) && hit.transform.name == "Portal Wall")
		{
			CreatePortal(index, hit);
		}
	}

	private void CreatePortal(int index, RaycastHit hit)
	{

		if (portals[index] == null)
		{
			portals[index] = CreatePortalObject(hit, index);
			portals[index].AddComponent<Portal>();
			portals[index].GetComponent<Portal>().SetPortalCamera(CreatePortalCamera());
			portals[index].GetComponent<Portal>().SetIndex(index);


			if (portalPrefab != null)
				portals[index].transform.GetChild(0).GetChild(0).GetChild(0).GetComponent<MeshRenderer>().material.color = portalColors[index];

		}
		else
		{
			portals[index].transform.position = ClampPosition(portals[index], hit);

			RotatePortal(portals[index], hit);
		}

		portals[index].GetComponent<Portal>().SetPortalWall(hit.transform.gameObject);

		if (portals[1 - index] != null)
		{
			GameObject portalObjectA = portals[index];
			GameObject portalObjectB = portals[1 - index];

			portals[index].GetComponent<Portal>().GetPortalCamera().targetTexture = new RenderTexture(Screen.width, Screen.height, 24);

			if (portalPrefab != null)
				portalObjectB.transform.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().material.mainTexture = portals[index].GetComponent<Portal>().GetPortalCamera().targetTexture;
			else
				portalObjectB.GetComponent<MeshRenderer>().material.mainTexture = portals[index].GetComponent<Portal>().GetPortalCamera().targetTexture;

			portals[1 - index].GetComponent<Portal>().GetPortalCamera().targetTexture = new RenderTexture(Screen.width, Screen.height, 24);

			if (portalPrefab != null)
				portalObjectA.transform.GetChild(0).GetChild(0).GetComponent<MeshRenderer>().material.mainTexture = portals[1 - index].GetComponent<Portal>().GetPortalCamera().targetTexture;
			else
				portalObjectA.GetComponent<MeshRenderer>().material.mainTexture = portals[1 - index].GetComponent<Portal>().GetPortalCamera().targetTexture;

			portals[index].GetComponent<Portal>().GetPortalCamera().enabled = true;
			portals[1 - index].GetComponent<Portal>().GetPortalCamera().enabled = true;
		}

	}

	private void RotatePortal(GameObject portal, RaycastHit hit)
	{
		portal.transform.rotation = Quaternion.Euler(hit.transform.localRotation.eulerAngles.x, hit.transform.eulerAngles.y, hit.transform.eulerAngles.z);
	}

	private Vector3 ClampPosition(GameObject portal, RaycastHit hit)
	{
		float wallWidth = hit.transform.localScale.x;
		float wallHeight = hit.transform.localScale.y;

		float portalWidth = portal.transform.localScale.x;
		float portalHeight = portal.transform.localScale.y;

		float widthRatio = portalWidth / wallWidth;
		float heightRatio = portalHeight / wallHeight;

		float xMin = widthRatio / 2f;
		float xMax = 1 - widthRatio / 2f;

		float yMin = heightRatio / 2f;
		float yMax = 1 - heightRatio / 2f;

		float clampedY = Mathf.Clamp(hit.textureCoord.y, yMin, yMax);

		Vector3 worldPoint = hit.point;

		if (hit.textureCoord.x < xMin || hit.textureCoord.x > xMax)
		{
			Vector3 horizontalDirection = hit.point - hit.transform.position;
			Debug.Log(horizontalDirection);
			float shiftAmount = wallWidth / 2f - portalWidth / 2f;
			worldPoint = hit.transform.position + horizontalDirection * shiftAmount / 10f;
		}

		float worldY = hit.transform.position.y - hit.transform.localScale.y / 2 + clampedY * hit.transform.localScale.y;
		worldPoint.y = worldY;

		return worldPoint;
	}

	private GameObject CreatePortalObject(RaycastHit hit, int index)
	{
		GameObject newPortalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
		newPortalObject.name = "Portal Object";
		newPortalObject.transform.localScale = new Vector3(3f, 3f, 3f);

		newPortalObject.transform.position = ClampPosition(newPortalObject, hit);

		newPortalObject.GetComponent<MeshCollider>().convex = true;
		newPortalObject.GetComponent<MeshCollider>().isTrigger = true;

		if (portalPrefab != null)
		{
			GameObject prefabObject = Instantiate(portalPrefab, Vector3.zero, Quaternion.identity);
			prefabObject.transform.SetParent(newPortalObject.transform);
			prefabObject.transform.localPosition = Vector3.zero;
			prefabObject.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

			prefabObject.transform.GetChild(0).GetComponent<MeshRenderer>().material = new Material(cutoutShader);
			newPortalObject.GetComponent<MeshRenderer>().enabled = false;
		}
		else
			newPortalObject.GetComponent<MeshRenderer>().material = new Material(cutoutShader);

		RotatePortal(newPortalObject, hit);

		return newPortalObject;
	}

	private Camera CreatePortalCamera()
	{
		GameObject portalCamera = new GameObject();
		portalCamera.AddComponent<Camera>();
		portalCamera.GetComponent<Camera>().enabled = false;
		portalCamera.name = "Portal Camera";

		return portalCamera.GetComponent<Camera>();
	}

	private void ReorientPortal(int index)
	{
		GameObject portalObjectA = portals[index];
		GameObject portalObjectB = portals[1 - index];

		Vector3 angleDifference = Vector3.up * (180f + (portalObjectB.transform.rotation.eulerAngles.y - portalObjectA.transform.rotation.eulerAngles.y));
		portals[1 - index].GetComponent<Portal>().GetPortalCamera().transform.rotation = Quaternion.Euler(Camera.main.transform.rotation.eulerAngles + angleDifference);

		Vector3 portalVector = portalObjectA.transform.position - Camera.main.transform.position;
		portalVector = Quaternion.Euler(angleDifference) * portalVector;
		portals[1 - index].GetComponent<Portal>().GetPortalCamera().transform.position = portalObjectB.transform.position - portalVector;
	}

}
