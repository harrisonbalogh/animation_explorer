using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour {

    [SerializeField]
    private float JumpForce = 40;
    [SerializeField]
    private float JumpUpwardsDampener = 0.5f;
    [SerializeField]
    private float Acceleration = 20;
    [SerializeField]
    private float Deceleration = 40;
    [SerializeField]
    private float MAX_SPEED = 100;
    [SerializeField]
    private float LookAmplifier_X = 5;
    [SerializeField]
    private float LookAmplifier_Y = 3;
    [SerializeField]
    private float GRAVITY = -5;
    [SerializeField]
    private float MAX_LOOK_UP = 20;
    [SerializeField]
    private float MAX_LOOK_DOWN = 15;
    //[SerializeField]
    //private float MAX_HEAD_TURN = 20;
    [SerializeField]
    private float strafe_dampener = 0.5f;
    [SerializeField]
    private float backwards_dampener = 0.4f;
    [SerializeField]
    private float turn_rate = 1;
    [SerializeField]
    private Text ui_controlStyleLabel;

    private CharacterController _controller;
    [SerializeField]
    private GameObject _shoulderPoint;
    [SerializeField]
    private Camera overviewCamera;
    [SerializeField]
    private GameObject _centerPoint;
    private Vector3 _velocity;

    public LayerMask Ground;

    private int movementMode;

    private int locationRecorderIndex = 0;
    private GameObject[] locationRecorders = new GameObject[1000];
    private GameObject[] locationRecordersCyl = new GameObject[1000];

    [SerializeField]
    private GameObject debugSpherePrefab;

	// Use this for initialization
	void Start () {

        _controller = GetComponent<CharacterController>();
        movementMode = 1;
        ui_controlStyleLabel.text = "Targeted";

        for (int i = 0; i < locationRecorders.Length; i++)
        {
            locationRecorders[i] = (GameObject)Instantiate(debugSpherePrefab);
            locationRecorders[i].transform.parent = this.transform.parent;
            locationRecordersCyl[i] = locationRecorders[i].transform.Find("Pointer").gameObject;
        }

    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        //Gizmos.DrawWireSphere(_centerPoint.transform.position, 0.2f);
        Gizmos.DrawWireCube(_centerPoint.transform.position, new Vector3(0.2f, 0.2f, 0.2f));
    }
    
    // Update is called once per frame
    void Update () {
        if (Input.GetButtonDown("2"))
        {
            movementMode = (movementMode + 1) % 2;
            _velocity = new Vector3();
            _shoulderPoint.transform.localEulerAngles = new Vector3(0, 0, _shoulderPoint.transform.localEulerAngles.z);

            if (movementMode == 0)
                ui_controlStyleLabel.text = "FPS";
            else if (movementMode == 1)
                ui_controlStyleLabel.text = "Targeted";
                
        }

        if (movementMode == 0)
        {
            handleMovement_fps();
        } else if (movementMode == 1)
        {
            handleMovement_targeted();
        }

        // Sample terrain
        //Terrain t = Terrain.activeTerrain;
        //Debug.Log(t.SampleHeight(transform.position));

    }

    void handleMovement_fps()
    {

        bool _isGrounded = Physics.CheckBox(_centerPoint.transform.position, new Vector3(0.2f, 0.2f, 0.2f), transform.rotation, Ground, QueryTriggerInteraction.Ignore);

        Vector3 userInput = new Vector3(0, 0, 0);

        if (_isGrounded)
        {
            _velocity.y = 0f; // Reset gravity affect

            // User Input
            userInput = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * MAX_SPEED;
            userInput = transform.TransformDirection(userInput);
            _velocity = userInput;

            // Jumping
            if (Input.GetButtonDown("Jump"))
            {
                _velocity.y = JumpForce; // Could make variable 'JumpHeight' instead of force but requires sqrt calc.
            }

        }
        else
        {
            // Gravity application
            if (_velocity.y > 0)
                _velocity.y += (float)(GRAVITY * JumpUpwardsDampener);
            else
                _velocity.y += GRAVITY;
        }
        // Apply velocity
        if (_velocity != Vector3.zero)
        {
            _controller.Move(_velocity * Time.deltaTime);
            if (userInput.x == 0 && userInput.z == 0 && _isGrounded)
            {
                Vector2 xzVel = new Vector2(_velocity.x, _velocity.z);
                Vector2 zxVelReduced = xzVel.normalized * Mathf.Max(xzVel.magnitude - Deceleration, 0); // slow down
                _velocity = new Vector3(zxVelReduced.x, _velocity.y, zxVelReduced.y);
            }

            locationRecorders[locationRecorderIndex].transform.position = new Vector3(this.transform.position.x, this.transform.position.y - 1, this.transform.position.z);
            locationRecordersCyl[locationRecorderIndex].transform.Rotate(_velocity);
            locationRecorderIndex = (locationRecorderIndex + 1) % locationRecorders.Length;
        }

        // User Look Input
        float yaw = Input.GetAxis("Horizontal_R") * LookAmplifier_X;
        transform.Rotate(new Vector3(0, yaw, 0));
        float pitch = Input.GetAxis("Vertical_R") * LookAmplifier_Y + _shoulderPoint.transform.localEulerAngles.x;
        // Clamp up/down looking
        if (pitch < 180)
        {
            if (pitch > MAX_LOOK_DOWN)
            {
                pitch = MAX_LOOK_DOWN;
            }
        }
        else if (pitch >= 180)
        {
            if (pitch < 360 - MAX_LOOK_UP)
            {
                pitch = 360 - MAX_LOOK_UP;
            }
        }
        // TODO: smooth damp this
        _shoulderPoint.transform.localEulerAngles = new Vector3(pitch, _shoulderPoint.transform.localEulerAngles.y, _shoulderPoint.transform.localEulerAngles.z);

    }

    void handleMovement_targeted()
    {
        // The right stick acts as a target, and the left stick moves the character based on the target point.
        // Think of a game where a target is locked, and pushing the stick left (90deg) would circle you around the target at the same radius.
        // The character would stay perpendicular to the target when the stick is pushed left (90deg).
        // Basically making the 'forward' vector aim in the targeted direction or point.

        bool _isGrounded = Physics.CheckBox(_centerPoint.transform.position, new Vector3(0.2f, 0.2f, 0.2f), transform.rotation, Ground, QueryTriggerInteraction.Ignore);

        Vector3 userInput = new Vector3(0, 0, 0);

        Vector3 preserveAngle = _shoulderPoint.transform.eulerAngles;

        if (_isGrounded) {
            _velocity.y = 0f; // Reset gravity affect

            // User Input
            userInput = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * MAX_SPEED;
            float inputAngle = -Vector3.SignedAngle(userInput, Vector3.forward, Vector3.up);
            float cameraAngle = _shoulderPoint.transform.eulerAngles.y;
            if (overviewCamera.enabled) {
                cameraAngle = overviewCamera.transform.eulerAngles.y;
            }
            Vector3 impulse = Quaternion.Euler(0, inputAngle + cameraAngle, 0) * Vector3.forward;

            _velocity = impulse * userInput.magnitude; // +=
            //_velocity = Vector3.ClampMagnitude(_velocity, MAX_SPEED);

            // Jumping
            if (Input.GetButtonDown("Jump"))
            {
                _velocity.y = JumpForce; // Could make variable 'JumpHeight' instead of force but requires sqrt calc.
            }

        }
        // Apply velocity
        if (_velocity != Vector3.zero)
        {
            // Move character by absolute vector _velocity
            _controller.Move(_velocity * Time.deltaTime);
            // Point character in direction of velocity
            transform.rotation = Quaternion.LookRotation(new Vector3(_velocity.x, 0, _velocity.z));
            // Keep shoulder camera pointed in constant direction
            _shoulderPoint.transform.eulerAngles = preserveAngle;

            if (userInput.x == 0 && userInput.z == 0 && _isGrounded)
            {
                Vector2 xzVel = new Vector2(_velocity.x, _velocity.z);
                Vector2 zxVelReduced = xzVel.normalized * Mathf.Max(xzVel.magnitude - Deceleration, 0); // slow down
                _velocity = new Vector3(zxVelReduced.x, _velocity.y, zxVelReduced.y);
            }

            locationRecorders[locationRecorderIndex].transform.position = new Vector3(this.transform.position.x, this.transform.position.y - 1, this.transform.position.z) ;
            locationRecordersCyl[locationRecorderIndex].transform.Rotate(_velocity);
            locationRecorderIndex = (locationRecorderIndex+1) % locationRecorders.Length;
        }

        if (!_isGrounded)
        {
            // Gravity application
            if (_velocity.y > 0)
                _velocity.y += (float)(GRAVITY * JumpUpwardsDampener);
            else
                _velocity.y += GRAVITY;
        }

        // User Look Input
        float yaw = Input.GetAxis("Horizontal_R") * LookAmplifier_X + _shoulderPoint.transform.localEulerAngles.y;
        _shoulderPoint.transform.localEulerAngles = new Vector3(_shoulderPoint.transform.localEulerAngles.x, yaw, _shoulderPoint.transform.localEulerAngles.z);
        float pitch = Input.GetAxis("Vertical_R") * LookAmplifier_Y + _shoulderPoint.transform.localEulerAngles.x;
        // Clamp up/down looking
        if (pitch < 180)
        {
            if (pitch > MAX_LOOK_DOWN)
            {
                pitch = MAX_LOOK_DOWN;
            }
        }
        else if (pitch >= 180)
        {
            if (pitch < 360 - MAX_LOOK_UP)
            {
                pitch = 360 - MAX_LOOK_UP;
            }
        }
        _shoulderPoint.transform.localEulerAngles = new Vector3(pitch, _shoulderPoint.transform.localEulerAngles.y, _shoulderPoint.transform.localEulerAngles.z);


    }
}
