using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
#if VARWINCLIENT
using Varwin.Desktop;
using IngameDebugConsole;
#endif
using Varwin.PlatformAdapter;
using Varwin.Public;

namespace Varwin.DesktopPlayer
{
    [RequireComponent(typeof(DesktopPlayerInput), typeof(DesktopPlayerInteractionController))]
    public class DesktopPlayerController : MonoBehaviour, IPlayerController
    {
        private const float MaxFallingCheckoutDistance = 1000f;
        private const float ExtraBodyWidth = 0.05f;
        private const float PlayerOnGroundHeight = 0.1f;
        private const int HitsCount = 100;

        public static DesktopPlayerController Instance { get; private set; }
        
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsFalling { get; private set; }
        
        public Quaternion Rotation
        {
            get => Quaternion.Euler(_currentRotation);
            set => SetRotation(value);
        }
        
        public Vector3 Position
        {
            get => transform.position;
            set => SetPosition(value);
        }

        public bool PlayerCursorIsVisible = true;

        [Header("Interaction")]
        [SerializeField]
        public LayerMask RaycastIgnoreMask;
        public Camera PlayerCamera;
        public GameObject Hand;
        
        [Header("Camera")] 
        [SerializeField]
        private float _normalFieldOfView = 60f;
        [SerializeField]
        private float _sprintFieldOfViewChange = 2f;
        
        [Header("Body")]
        [SerializeField]
        private float _bodyRadius = 0.15f;

        [Header("Movement")]
        [SerializeField]
        private float _sprintSpeedMultiplier = 2f;
        [SerializeField]
        private float _crouchSpeedMultiplier = 0.3f;
        [SerializeField]
        private float _maximumStepHeight = 0.5f;
        [SerializeField]
        private float maximumStepLength = 1f;
        
        [Header("Rotation")]
        [SerializeField]
        private float _normalRotationSpeed = 2.0f;
        [SerializeField]
        private float _sprintRotationSpeed = 1.0f;

        private float _jumpSpeed => Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * PlayerManager.JumpHeight);
        
        private float _rotationSpeed;
        private float _speed;

        private float _currentFieldOfView;
        
        private Vector3 _targetPosition;
        private float _targetLevel;

        private Vector3 _beforeJumpFootPosition;

        private Vector3 _cameraLocalPosition;
        private Vector3 _currentRotation;
        
        private DesktopPlayerInput _playerInput;
        private DesktopPlayerInteractionController _interactionController;

        private Vector3 HeadPosition => transform.position + Vector3.up * PlayerManager.PlayerNormalHeight;

        private readonly RaycastHit[] _hits = new RaycastHit[HitsCount];
        private readonly RaycastHit[] _obstacleHits = new RaycastHit[HitsCount];

        private Collider _blockingCollider;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
            }
            
            PlayerManager.PlayerRespawned += OnPlayerRespawned;
            ProjectData.GameModeChanged += OnGameModeChanged;
            
            gameObject.SetActive(ProjectData.GameMode != GameMode.Edit);

            FieldOfView = _normalFieldOfView;
        }

        private void Start()
        {
            _playerInput = GetComponent<DesktopPlayerInput>();
            _interactionController = GetComponent<DesktopPlayerInteractionController>();
            InputAdapter.Instance.PlayerController.PlayerTeleported += PlayerControllerOnPlayerTeleported;
            
            if (PlayerCamera)
            {
                _cameraLocalPosition = PlayerCamera.transform.localPosition;
                _currentRotation = PlayerCamera.transform.rotation.eulerAngles;
            }

            _rotationSpeed = _normalRotationSpeed;
            _speed = 0f;

            _targetPosition = transform.position;
            
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (PlayerCamera)
            {
                _cameraLocalPosition = PlayerCamera.transform.localPosition;
                _currentRotation = PlayerCamera.transform.rotation.eulerAngles;
            }

            SetPosition(transform.position);
            SetRotation(transform.rotation);
        }

        private void OnDisable()
        {
            ResetCursor();
        }

        private void OnDestroy()
        {
            InputAdapter.Instance.PlayerController.PlayerTeleported -= PlayerControllerOnPlayerTeleported;
            PlayerManager.PlayerRespawned -= OnPlayerRespawned;
            ProjectData.GameModeChanged -= OnGameModeChanged;
        }

        private void OnGameModeChanged(GameMode gameMode)
        {
            if (gameMode == GameMode.Edit)
            {
                PlayerManager.Respawn();
            }

            gameObject.SetActive(gameMode != GameMode.Edit);
        }

        private void Update()
        {
            if (ProjectData.GameMode == GameMode.Undefined)
            {
                return;
            }
            
            _interactionController.SetCursorVisibility(PlayerCursorIsVisible && !Cursor.visible && PlayerManager.CursorIsVisible);
            
#if VARWINCLIENT
            if (DesktopPopupManager.IsPopupShown || DesktopEscapeMenu.IsActive)
            {
                ResetCursor();
                return;
            }

            if (DebugLogManager.Instance && DebugLogManager.Instance.IsLogWindowVisible)
            {
                ResetCursor();
                return;
            }
#endif
            
            UpdateInput();
            UpdateMovement();
            UpdateCamera();
            UpdatePosition();
            
            Cursor.visible = ProjectData.IsMultiplayerSceneActive;
        }

        private void ResetCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnPlayerRespawned()
        {
            SetPosition(PlayerAnchorManager.SpawnPoint.position);
            SetRotation(PlayerAnchorManager.SpawnPoint.rotation);
        }

        private void PlayerControllerOnPlayerTeleported(Vector3 position)
        {
            SetPosition(position);
        }

        private void UpdateInput()
        {
            if (!IsFalling)
            {
                if (_playerInput.IsCrouching)
                {
                    IsCrouching = true;
                }
                else
                {
                    if (IsCrouching)
                    {
                        bool raycastUp = Physics.Raycast(
                            PlayerCamera.transform.position,
                            transform.TransformDirection(Vector3.up),
                            out RaycastHit hitUp,
                            PlayerManager.PlayerNormalHeight - PlayerManager.PlayerCrouchHeight, ~RaycastIgnoreMask);
                        
                        var spherePosition = PlayerCamera.transform.position + (PlayerManager.PlayerNormalHeight - PlayerManager.PlayerCrouchHeight - _bodyRadius) * Vector3.up;

                        if (!Physics.CheckSphere(spherePosition, 0.8f * _bodyRadius, ~RaycastIgnoreMask, QueryTriggerInteraction.Ignore)
                            && (!raycastUp || raycastUp && hitUp.collider.isTrigger))
                        {
                            IsCrouching = false;
                        }
                    }
                }
            }

            IsSprinting = _playerInput.IsSprinting;

            if (_playerInput.IsJumping && !IsCrouching)
            {
                if (!IsFalling && !IsJumping)
                {
                    IsJumping = true;
                    IsFalling = true;
                }
            }
        }
        
        private void UpdateMovement()
        {
            if (!PlayerManager.WasdMovementEnabled || !_playerInput.IsMoving)
            {
                return;
            }

            Vector2 offset = _playerInput.PlayerMovement;

            if (offset.sqrMagnitude > 1f)
            {
                offset = offset.normalized;
            }

            var positionOffset = new Vector3(offset.y, 0.0f, offset.x);

            var targetMovementSpeed = PlayerManager.WalkSpeed;
            if (IsCrouching)
            {
                targetMovementSpeed = PlayerManager.CrouchSpeed;
            }
            else if (IsSprinting)
            {
                targetMovementSpeed = PlayerManager.SprintSpeed;
            }

            _speed = Mathf.Lerp(_speed, targetMovementSpeed, 10f * Time.deltaTime);

            MoveIfPossibleTo(_speed * Time.deltaTime * positionOffset);

        }

        private void UpdateCamera()
        {
            float cameraHeight = Mathf.Lerp(PlayerCamera.transform.localPosition.y, _cameraLocalPosition.y, 10f * Time.deltaTime);
            PlayerCamera.transform.localPosition = new Vector3(_cameraLocalPosition.x, cameraHeight, _cameraLocalPosition.z);

            UpdateFieldOfView();
            
            Vector2 cursorInput = _playerInput.Cursor;
            
            _rotationSpeed = Mathf.Lerp(_rotationSpeed, IsSprinting ? _sprintRotationSpeed : _normalRotationSpeed, 10f * Time.deltaTime);
            
            if (!_playerInput.IsCameraFixed && PlayerManager.MouseLookEnabled)
            {
                Cursor.lockState = Application.isFocused && !ProjectData.IsMultiplayerSceneActive ? CursorLockMode.Locked : CursorLockMode.None;            
                if (!_interactionController.IsRotatingObject)
                {
                    _currentRotation.x -= cursorInput.y * _rotationSpeed;
                    _currentRotation.y += cursorInput.x * _rotationSpeed;
                    
                    SetRotation(_currentRotation);

                    if (IsCrouching)
                    {
                        _cameraLocalPosition = PlayerManager.PlayerCrouchHeight * Vector3.up;
                    }
                    else
                    {
                        _cameraLocalPosition = PlayerManager.PlayerNormalHeight * Vector3.up;
                    }
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void UpdateFieldOfView()
        {
            float targetFieldOfView = _currentFieldOfView;
            float fieldOfViewLerpSpeed = 10f;
            
            if (_playerInput.IsMoving && IsSprinting)
            {
                targetFieldOfView += _sprintFieldOfViewChange;
                fieldOfViewLerpSpeed = 5f;
            }
            
            PlayerCamera.fieldOfView = Mathf.Lerp(PlayerCamera.fieldOfView, targetFieldOfView, fieldOfViewLerpSpeed * Time.deltaTime);
        }

        public float FieldOfView
        {
            get => _currentFieldOfView;
            set
            {
                PlayerCamera.fieldOfView = value;
                _currentFieldOfView = value;
            }
        }

        private void MoveIfPossibleTo(Vector3 moveDelta)
        {
            var cameraPos = PlayerCamera.transform.position;
            var directedDelta = transform.rotation * moveDelta;
            
            Vector3 futurePosition = transform.position + directedDelta;

            RaycastHit hitDown = GetHighestHit(GetSphereCast(cameraPos + directedDelta, Vector3.down, _bodyRadius + ExtraBodyWidth, MaxFallingCheckoutDistance));

            if (!hitDown.Equals(default(RaycastHit)))
            {
                bool noObstacles = IsPossibleToMove(directedDelta);
                if (noObstacles)
                {
                    if (hitDown.transform.CompareTag("TeleportArea") && PlayerCamera.transform.localPosition.y - hitDown.distance <= _maximumStepHeight + _bodyRadius)
                    {
                        _targetPosition = futurePosition;
                        _targetPosition.y = hitDown.point.y;
                    }
                }
            }
        }

        private int BodyCapsuleOverlaps(Vector3 footPosition, Vector3 direction, float distance)
        {
            var capsuleDownPoint = footPosition + Vector3.up * (_maximumStepHeight );
            var capsuleUpPoint = HeadPosition;

            return Physics.CapsuleCastNonAlloc(
                capsuleUpPoint,
                capsuleDownPoint,
                _bodyRadius,
                direction,
                _obstacleHits,
                distance,
                ~RaycastIgnoreMask,
                QueryTriggerInteraction.Ignore
                );
        }

        private bool IsPossibleToMove(Vector3 moveDirection)
        {
            Vector3 startPos = HeadPosition;
            Vector3 normalizedDirection = moveDirection.normalized;
            Vector3 endPos = startPos + normalizedDirection;

            Vector3 footPosition = startPos + Vector3.down * PlayerManager.PlayerNormalHeight;
            var capsuleIntersections = BodyCapsuleOverlaps(footPosition, normalizedDirection, moveDirection.magnitude);
            
            ObjectController grabParent = null;
            GameObject[] grabChildren = null;
            
            if (_interactionController && _interactionController.GetGrabbedObject())
            {
                grabParent = _interactionController.GetGrabbedObject().GetWrapper()?.GetObjectController()?.LockParent;
                grabChildren = grabParent?.Descendants?.Select(x => x.gameObject).ToArray();
            }
            
            bool hasParent = grabParent != null && grabParent.gameObject;
            bool hasChildren = grabChildren != null && grabChildren.Length > 0;

            for (int index = 0; index < capsuleIntersections; index++)
            {
                var hit = _obstacleHits[index];

                if (hit.collider && hit.collider.gameObject)
                {
                    bool hitParent = hasParent && hit.collider.gameObject == grabParent.gameObject;
                    bool hitChildren = hasChildren && grabChildren.Contains(hit.collider.gameObject);

                    if (hitParent || hitChildren)
                    {
                        capsuleIntersections--;
                    }
                }
            }

            bool hasSideObstacle = capsuleIntersections > 0;

            if (!IsJumping && !IsFalling)
            {
                _beforeJumpFootPosition = footPosition;
            }

            const int raycastCount = 10;

            for (var i = 0; i < raycastCount; ++i)
            {
                Vector3 checkingPosition = Vector3.Lerp(startPos, endPos, (float) i / (raycastCount - 1));

                int hitForward = Physics.RaycastNonAlloc(checkingPosition,
                    normalizedDirection,
                    _hits,
                    Vector3.Distance(checkingPosition, startPos),
                    ~RaycastIgnoreMask,
                    QueryTriggerInteraction.Ignore);

                if (hitForward > 0)
                {
                    break;
                }

                RaycastHit hitDown = GetHighestHit(Physics.RaycastNonAlloc(checkingPosition,
                    transform.TransformDirection(Vector3.down),
                    _hits,
                    MaxFallingCheckoutDistance,
                    ~RaycastIgnoreMask,
                    QueryTriggerInteraction.Ignore));
                
                bool feetAreaFlatEnough = Vector3.Angle(hitDown.normal, Vector3.up) < PlayerManager.TeleportAngleLimit;
                bool canClimb = hitDown.point.y < (footPosition.y + _maximumStepHeight);

                if (!feetAreaFlatEnough && IsJumping)
                {
                    feetAreaFlatEnough = hitDown.point.y < _beforeJumpFootPosition.y;
                }

                if (!canClimb)
                {
                    return false;
                }

                if (feetAreaFlatEnough && !hasSideObstacle)
                {
                    return true;
                }
            }

            return false;
        }

        private RaycastHit GetHighestHit(int size, RaycastHit[] hits = null)
        {
            if(size == 0)
            {
                return default;
            }

            if (hits == null)
            {
                hits = _hits;
            }

            RaycastHit ret = default;

            float yMax = float.MinValue;

            ObjectController grabParent = null;
            GameObject[] grabChildren = null;

            if (_interactionController && _interactionController.GetGrabbedObject())
            {
                grabParent = _interactionController.GetGrabbedObject().GetWrapper()?.GetObjectController()?.LockParent;
                grabChildren = grabParent?.Descendants?.Select(x => x.gameObject).ToArray();
            }

            bool hasParent = grabParent != null && grabParent.gameObject;
            bool hasChildren = grabChildren != null && grabChildren.Length > 0;

            for (int index = 0; index < size; index++)
            {
                RaycastHit hit = hits[index];

                if (hit.collider && hit.collider.gameObject)
                {
                    bool hitParent = hasParent && hit.collider.gameObject == grabParent.gameObject;
                    bool hitChildren = hasChildren && grabChildren.Contains(hit.collider.gameObject);

                    if (hitParent || hitChildren)
                    {
                        break;
                    }
                }

                if (hit.collider.isTrigger && hit.rigidbody)
                {
                    continue;
                }

                if (hit.point.y > yMax && hit.collider.gameObject.CompareTag("TeleportArea"))
                {
                    ret = hit;
                    yMax = ret.point.y;
                }
            }

            return ret;
        }
        
        private void UpdatePosition()
        {
            if (ProjectData.PlatformMode == PlatformMode.Vr)
            {
                transform.position = _targetPosition;
                return;
            }

            float y = transform.position.y;
            float deltaY = Mathf.Abs(y - _targetPosition.y);

            if (PlayerManager.UseGravity)
            {
                if (transform.position.y < PlayerManager.RespawnHeight)
                {
                    PlayerManager.Respawn();
                }

                var highestHit = GetSphereHighestHit();
                if (!IsJumping && !IsFalling)
                {
                    CalculateOnGroundTargetY(highestHit);
                }

                if (!IsFalling)
                {
                    if (y < _targetPosition.y)
                    {
                        y = Mathf.Lerp(y, _targetPosition.y, 8f * Time.deltaTime);
                    }
                    else if (y > _targetPosition.y)
                    {
                        if (deltaY <= _maximumStepHeight)
                        {
                            y = Mathf.Lerp(y, _targetPosition.y, 8f * Time.deltaTime);
                        }
                        else
                        {
                            IsFalling = true;
                        }
                    }

                    PlayerManager.FallingTime = 0;
                }
                else
                {
                    y = CalculateFallingY(highestHit);
                }
            }
            else
            {
                IsFalling = false;
                IsJumping = false;
                PlayerManager.FallingTime = 0;
            }

            transform.position = new Vector3(_targetPosition.x, y, _targetPosition.z);
        }

        private void CalculateOnGroundTargetY(RaycastHit highestHit)
        {
            if (highestHit.Equals(default(RaycastHit)))
            {
                IsFalling = true;
                return;
            }

            var headToHitDistance = Vector3.Distance(HeadPosition, highestHit.point);

            if (headToHitDistance > PlayerManager.PlayerNormalHeight + PlayerOnGroundHeight)
            {
                IsFalling = true;
                _targetPosition.y = highestHit.point.y;
                return;
            }

            if (highestHit.transform && highestHit.transform.CompareTag("TeleportArea") && highestHit.collider && !highestHit.collider.isTrigger)
            {
                IsJumping = false;
                IsFalling = false;
                _targetPosition.y = highestHit.point.y;
            }
        }

        private float CalculateFallingY(RaycastHit highestHit)
        {
            PlayerManager.FallingTime += Time.deltaTime;

            float y = transform.position.y;
            float fallingDelta = Physics.gravity.y * PlayerManager.FallingTime * Time.deltaTime;

            if (IsJumping)
            {
                float jumpDelta = _jumpSpeed * Time.deltaTime;

                if (fallingDelta < jumpDelta)
                {
                    bool raycastUp = Physics.Raycast(
                        PlayerCamera.transform.position,
                        Vector3.up,
                        out RaycastHit hitUp,
                        jumpDelta, ~RaycastIgnoreMask);

                    var spherePosition = PlayerCamera.transform.position + (jumpDelta - _bodyRadius) * Vector3.up;

                    if (Physics.CheckSphere(spherePosition, _bodyRadius, ~RaycastIgnoreMask, QueryTriggerInteraction.Ignore) || raycastUp && !hitUp.collider.isTrigger)
                    {
                        IsJumping = false;
                        jumpDelta = 0f;
                    }
                }

                fallingDelta += jumpDelta;
            }

            if (y + fallingDelta > _targetPosition.y)
            {
                y += fallingDelta;
            }
            else
            {
                IsFalling = !((HeadPosition.y - PlayerManager.PlayerNormalHeight - PlayerOnGroundHeight) < highestHit.point.y);
                IsJumping = false;
                y = _targetPosition.y;
                PlayerManager.FallingTime = 0;
            }

            return y;
        }

        private RaycastHit GetSphereHighestHit()
        {
            var fromHeadRaycastIntersections = GetSphereCast(HeadPosition, Vector3.down, _bodyRadius, float.PositiveInfinity);
            
            return GetHighestHit(fromHeadRaycastIntersections, _hits);
        }
        

        private int GetSphereCast(Vector3 origin, Vector3 direction, float radius, float distance)
        {
            return Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _hits,
                distance,
                ~RaycastIgnoreMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private RaycastHit GetHitDown()
        {
            return GetHitDown(_maximumStepHeight);
        }
        
        private RaycastHit GetHitDown(float height)
        {
            Vector3 playerFootPosition = PlayerCamera.transform.position + Vector3.down * PlayerManager.PlayerNormalHeight;
            return GetHighestHit(Physics.RaycastNonAlloc(
                playerFootPosition + _maximumStepHeight * Vector3.up,
                Vector3.down,
                _hits,
                height,
                ~RaycastIgnoreMask,
                QueryTriggerInteraction.Ignore));
        }

        public void ResetCameraState()
        {
            _currentRotation = Vector3.zero;
            PlayerCamera.transform.localRotation = Quaternion.identity;
        }

        public void SetPosition(Vector3 position)
        {
            _targetPosition = position;
            transform.position = position;
        }

        public void SetRotation(Quaternion rotation)
        {
            SetRotation(rotation.eulerAngles);
        }

        public void SetRotation(Vector3 rotation)
        {
            _currentRotation = rotation;
            
            if (_currentRotation.x >= 270f)
            {
                _currentRotation.x -= 360f;
            }
                    
            _currentRotation.y = Mathf.Repeat(_currentRotation.y, 360);
            _currentRotation.x = Mathf.Clamp(_currentRotation.x, -90, 90);
            
            transform.rotation = Quaternion.Euler(0, _currentRotation.y, 0);
            PlayerCamera.transform.localRotation = Quaternion.Euler(_currentRotation.x, 0, 0);
        }
        
        public void CopyTransform(Transform targetTransform)
        {
            SetRotation(targetTransform.rotation);
            SetPosition(targetTransform.position);
        }

        public void ForceGrabObject(GameObject gameObject)
        {
            if (_interactionController)
            {
                _interactionController.ForceGrabObject(gameObject);
            }
        }

        public void ForceDropObject(GameObject gameObject)
        {
            if (_interactionController)
            {
                _interactionController.ForceDropObject(gameObject);
            }
        }
        
        public void DropGrabbedObject(bool forced = false)
        {
            if (_interactionController)
            {
                if (forced)
                {
                    _interactionController.ForceDropObject();
                }
                else
                {
                    _interactionController.DropGrabbedObject();
                }
            }
        }
    }
}
