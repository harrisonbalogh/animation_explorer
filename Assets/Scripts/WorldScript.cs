using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorldScript : MonoBehaviour {

    [SerializeField]
    private Camera OverviewCamera;
    [SerializeField]
    private Camera ShoulderCamera;
    [SerializeField]
    private Transform Character;
    [SerializeField]
    private Text ui_cameraLabel;

    [SerializeField]
    private Terrain worldTerrain;

    [SerializeField]
    private GameObject debugCylinder;
    [SerializeField]
    private GameObject terrainPrefab;

    private Vector3 overviewTargetPosition;
    private float slerpTime = 0;

    [SerializeField]
    private int heightMapNodes = 10;

    private float[,] heightMapData;
    private GameObject[,] heightMapCylinders;
    private bool heightMapOn = false;
    private GameObject heightMapTerrain;
    private Terrain heightMapTerrainComponent;
    private bool HEIGHT_MAPPER = true;

    private GameObject[] groundObjects;

    // Use this for initialization
    void Start () {
        // Default camera selection
        ShoulderCamera.enabled = true;
        OverviewCamera.enabled = false;
        ui_cameraLabel.text = "Shoulder";

        // Record location of camera set by editor to snap back to position after camera zoom-out effect
        overviewTargetPosition = OverviewCamera.transform.position;

        if (HEIGHT_MAPPER) {

            // pre-process height map
            // heightMapTerrainComponent = worldTerrain;
            heightMapData = new float[worldTerrain.terrainData.heightmapResolution, worldTerrain.terrainData.heightmapResolution];
            heightMapCylinders = new GameObject[worldTerrain.terrainData.heightmapResolution, worldTerrain.terrainData.heightmapResolution];

            // float[,] heights = heightMapTerrainComponent.terrainData.GetHeights(0, 0, heightMapTerrainComponent.terrainData.heightmapResolution, heightMapTerrainComponent.terrainData.heightmapResolution);

            float NODES = heightMapNodes; //  heightMapTerrainComponent.terrainData.heightmapResolution;
            float WIDTH = worldTerrain.terrainData.size.x;
            float HEIGHT = worldTerrain.terrainData.size.z;
            int GROUND_LAYER = 9;
            int layerMask = 1 << GROUND_LAYER;
            float skyHeight = 100;

            for (int x = 0; x < NODES; x++) {
                for (int y = 0; y < NODES; y++) {

                    float increment = WIDTH / NODES;

                    float xPos = (float)x * increment - WIDTH / 2 + increment / 2;
                    float yPos = (float)y * increment - HEIGHT / 2 + increment / 2;

                    Vector3 from = new Vector3(xPos, skyHeight, yPos);
                    RaycastHit hit;
                    if (Physics.Raycast(from, Vector3.down, out hit, Mathf.Infinity, layerMask)) {
                        heightMapData[x, y] = skyHeight - hit.distance;
                        heightMapCylinders[x, y] = (GameObject)Instantiate(debugCylinder);
                        float cylinderNumber = x * NODES + y;
                        heightMapCylinders[x,y].GetComponent<Oscilate>().SetHeight(heightMapData[x, y], cylinderNumber);
                        heightMapCylinders[x, y].transform.position = new Vector3(xPos, 0, yPos);
                        heightMapCylinders[x, y].SetActive(false);
                        // int xIndex = (int)((x * increment + increment / 2) / WIDTH * heightMapTerrainComponent.terrainData.heightmapResolution);
                        // int yIndex = (int)((y * increment + increment / 2) / HEIGHT * heightMapTerrainComponent.terrainData.heightmapResolution);
                        // heights[yIndex, xIndex] = heightMapData[x, y] / heightMapTerrainComponent.terrainData.size.y;
                    }
                }
            }

            var groundList = new System.Collections.Generic.List<GameObject>();
            for (var c = 0; c < this.transform.childCount; c++) {
                GameObject child = this.transform.GetChild(c).gameObject;
                if (child.layer == 9) {
                    groundList.Add(child);
                }
            }
            groundObjects = groundList.ToArray();

            // heightMapTerrain.SetActive(false);

            // smooth terrain
            //int lastIndex = 0;
            //for (int x = 0; x < heightMapTerrainComponent.terrainData.heightmapWidth; x++) {
            //    for (int y = 0; y < heightMapTerrainComponent.terrainData.heightmapHeight; y++) {
            //        heights[x, y] = 0;
            //    }
            //}

            // heightMapTerrainComponent.terrainData.SetHeights(0, 0, heights);
        }

    }
	
	// Update is called once per frame
	void FixedUpdate () {

	}

    // int updated_heightmap_cyls = 0; // ugly optimization
    private void Update() {

        if (Input.GetButtonDown("JOY_Z") && HEIGHT_MAPPER) {
            // heightMapTerrain.SetActive(!heightMapTerrain.activeSelf);

            heightMapOn = !heightMapOn;

            if (heightMapOn) {


                // foreach (GameObject g in groundObjects) {
                //    g.SetActive(false);
                // }

                // heightMapTerrain.SetActive(true);

                for (int x = 0; x < heightMapNodes; x++) {
                   for (int y = 0; y < heightMapNodes; y++) {
                    //    heightMapCylinders[x, y].transform.localScale = new Vector3(1, heightMapData[x, y], 1);
                       heightMapCylinders[x, y].SetActive(true);
                    //    updated_heightmap_cyls = 0;

                   }
                }

            } else {


                // foreach (GameObject g in groundObjects) {
                //    g.SetActive(true);
                // }

                // heightMapTerrain.SetActive(false);

                for (int x = 0; x < heightMapNodes; x++) {
                   for (int y = 0; y < heightMapNodes; y++) {

                       heightMapCylinders[x, y].SetActive(false);

                   }
                }

            }
        }


        if (Input.GetButtonDown("LBumper")) {
            // worldTerrain.gameObject.SetActive(!worldTerrain.gameObject.active);
            worldTerrain.GetComponent<Terrain>().enabled = !worldTerrain.GetComponent<Terrain>().enabled;
        }

         // Toggle different cameras
        if (Input.GetButtonDown("1")) {

            ShoulderCamera.enabled = !ShoulderCamera.enabled;
            OverviewCamera.enabled = !OverviewCamera.enabled;

            // Overview camera has zoom-out effect that starts from the shoulder camera
            if (OverviewCamera.enabled)
            {
                ui_cameraLabel.text = "Overview";
                slerpTime = Time.time;
                OverviewCamera.transform.position = ShoulderCamera.transform.position;
                OverviewCamera.transform.eulerAngles = ShoulderCamera.transform.eulerAngles;
            } else
            {
                ui_cameraLabel.text = "Shoulder";
            }
        }

        // Overview camera follows character
        if (OverviewCamera.enabled) {

            OverviewCamera.transform.LookAt(Character);

            // Zoom-effect for overview camera, slerping from shoulder to overview
            if (OverviewCamera.transform.position != overviewTargetPosition)
            {
                OverviewCamera.transform.position = Vector3.Slerp(OverviewCamera.transform.position, overviewTargetPosition, (Time.time - slerpTime) / 0.5f);
            }
        }

        // if (heightMapOn && updated_heightmap_cyls < heightMapNodes * heightMapNodes) {

        //    for (int x = 0; x < heightMapNodes; x++) {
        //        for (int y = 0; y < heightMapNodes; y++) {

        //            if (heightMapCylinders[x, y].transform.localScale.y >= heightMapData[x, y]) {
        //                heightMapCylinders[x, y].transform.localScale = 
        //                     new Vector3(1, heightMapData[x, y], 1);
        //                updated_heightmap_cyls++;
        //                continue;
        //            }

        //            heightMapCylinders[x, y].transform.localScale += new Vector3(0, 0.03f, 0);
        //        }
        //    }

        // }

    }

    private void OnDrawGizmosSelected() {

        // guard
        if (heightMapNodes == 0) { return; }
        
        float NODES = heightMapNodes;
        int WIDTH = 60;
        int HEIGHT = 60;

        int GROUND_LAYER = 9;
        int layerMask = 1 << GROUND_LAYER;

        Gizmos.color = Color.red;

        for (float x = 0; x < NODES; x++) {
            for (float y = 0; y < NODES; y++) {

                float xPos = (float) x * ((float) WIDTH / NODES) - (float) WIDTH / 2;
                float yPos = (float) y * ((float) HEIGHT / NODES) - (float) HEIGHT / 2;

                Vector3 from = new Vector3(xPos, 100, yPos);

                RaycastHit hit;

                if (Physics.Raycast(from, Vector3.down, out hit, Mathf.Infinity, layerMask)) {

                    Gizmos.DrawRay(new Vector3(xPos, 100 - hit.distance + 1, yPos), new Vector3(0, 0.1f, 0));

                }

            }

        }
    }


}
