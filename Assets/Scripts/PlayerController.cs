using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move Parameters")]
    [SerializeField] private bool isWalking;
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] [Range(0f, 1f)] private float runstepLenghten = 0.7f;
    [SerializeField] private bool useFovKick = false;
    [SerializeField] private FOVKick fovKick = new FOVKick();
    [SerializeField] private float stepInterval = 5f;
    [SerializeField] private AudioClip[] footstepSounds;
    

    [Header("Jump Parameters")]
    [SerializeField] private float jumpSpeed = 10f;
    [SerializeField] private float stickToGroundForce = 10f;
    [SerializeField] private float gravityMultiplier = 2f;
    [SerializeField] private LerpControlledBob jumpBob = new LerpControlledBob();
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    
    
    [Header("Look Parameters")]
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private bool useHeadBob = true;
    [SerializeField] private CurveControlledBob headBob = new CurveControlledBob();

    private CharacterController _characterController;
    private CharacterController CharacterController 
        => _characterController ?? (_characterController = GetComponent<CharacterController>());

    private Camera _camera;
    private Camera MainCamera => _camera ?? (_camera = Camera.main);

    private bool _jump;
    private float _yRotation;
    private Vector2 _input;
    private Vector3 _moveDir = Vector3.zero;
    private CollisionFlags _collisionFlags;
    private bool _previouslyGrounded;
    private Vector3 _originalCameraPosition;
    private float _stepCycle;
    private float _nextStep;
    private bool _jumping;
    private AudioSource _audioSource; //make property based

    private void Start()
    {
        _originalCameraPosition = MainCamera.transform.localPosition;
        fovKick.Setup(MainCamera);
        headBob.Setup(MainCamera, stepInterval);
        _stepCycle = 0f;
        _nextStep = _stepCycle / 2f;
        _jumping = false;
        _audioSource = GetComponent<AudioSource>();
        mouseLook.Init(transform, MainCamera.transform);
    }

    private void Update()
    {
        RotateView();
        if (!_jump) _jump = Input.GetButtonDown("Jump");

        if (!_previouslyGrounded && CharacterController.isGrounded)
        {
            StartCoroutine(jumpBob.DoBobCycle());
            PlayLandingSound();
            _moveDir.y = 0f;
            _jumping = false;
        }

        if (!CharacterController.isGrounded && !_jumping && _previouslyGrounded) _moveDir.y = 0f;

        _previouslyGrounded = CharacterController.isGrounded;
    }


    private void PlayLandingSound()
    {
        _audioSource.clip = landSound;
        _audioSource.Play();
        _nextStep = _stepCycle + .5f;
    }

    private void FixedUpdate()
    {
        GetInput(out var speed);
        
        var desiredMove = transform.forward * _input.y + transform.right * _input.x;

        Physics.SphereCast(transform.position, CharacterController.radius, Vector3.down, out var hitInfo,
                           CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

        _moveDir.x = desiredMove.x * speed;
        _moveDir.z = desiredMove.z * speed;


        if (CharacterController.isGrounded)
        {
            _moveDir.y = -stickToGroundForce;

            if (_jump)
            {
                _moveDir.y = jumpSpeed;
                PlayJumpSound();
                _jump = false;
                _jumping = true;
            }
        }
        else _moveDir += Physics.gravity * gravityMultiplier * Time.fixedDeltaTime;
        
        _collisionFlags = CharacterController.Move(_moveDir * Time.fixedDeltaTime);

        ProgressStepCycle(speed);
        UpdateCameraPosition(speed);

        mouseLook.UpdateCursorLock();
    }

    private void PlayJumpSound()
    {
        _audioSource.clip = jumpSound;
        _audioSource.Play();
    }

    private void ProgressStepCycle(float speed)
    {
        if (CharacterController.velocity.sqrMagnitude > 0 && (_input.x != 0 || _input.y != 0))
        {
            _stepCycle += (CharacterController.velocity.magnitude + (speed * (isWalking ? 1f : runstepLenghten))) *
                         Time.fixedDeltaTime;
        }

        if (!(_stepCycle > _nextStep)) return;

        _nextStep = _stepCycle + stepInterval;

        PlayFootStepAudio();
    }


    private void PlayFootStepAudio()
    {
        if (!CharacterController.isGrounded) return;
        
        var n = Random.Range(1, footstepSounds.Length);
        _audioSource.clip = footstepSounds[n];
        _audioSource.PlayOneShot(_audioSource.clip);
        
        footstepSounds[n] = footstepSounds[0];
        footstepSounds[0] = _audioSource.clip;
    }


    private void UpdateCameraPosition(float speed)
    {
        Vector3 newCameraPosition;
        if (!useHeadBob) return;
        if (CharacterController.velocity.magnitude > 0 && CharacterController.isGrounded)
        {
            MainCamera.transform.localPosition =
                headBob.DoHeadBob(CharacterController.velocity.magnitude +
                                  (speed * (isWalking ? 1f : runstepLenghten)));
            newCameraPosition = MainCamera.transform.localPosition;
            newCameraPosition.y = MainCamera.transform.localPosition.y - jumpBob.Offset();
        }
        else
        {
            newCameraPosition = MainCamera.transform.localPosition;
            newCameraPosition.y = _originalCameraPosition.y - jumpBob.Offset();
        }
        MainCamera.transform.localPosition = newCameraPosition;
    }

    private void GetInput(out float speed)
    {
        // Read input
        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");

        var wasWalking = isWalking;

        isWalking = !Input.GetKey(KeyCode.LeftShift);

        speed = isWalking ? walkSpeed : runSpeed;
        _input = new Vector2(horizontal, vertical);

        if (_input.sqrMagnitude > 1) _input.Normalize();

        if (isWalking != wasWalking && useFovKick && CharacterController.velocity.sqrMagnitude > 0)
        {
            StopAllCoroutines();
            StartCoroutine(!isWalking ? fovKick.FOVKickUp() : fovKick.FOVKickDown());
        }
    }

    private void RotateView() => mouseLook.LookRotation(transform, MainCamera.transform);

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.collider.attachedRigidbody;

        if (_collisionFlags == CollisionFlags.Below) return;
        if (body == null || body.isKinematic) return;

        body.AddForceAtPosition(CharacterController.velocity * 0.1f, hit.point, ForceMode.Impulse);
    }
}
