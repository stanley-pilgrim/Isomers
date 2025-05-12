using UnityEngine;
using UnityEngine.SceneManagement;
using Varwin.PlatformAdapter;
using Varwin.Public;
using Varwin.SocketLibrary;

namespace Varwin.XR
{
    /// <summary>
    /// Контроллер управления игроком.
    /// </summary>
    public class VarwinXRPlayerMoveController : MonoBehaviour
    {
        /// <summary>
        /// Поскольку мы должны найти наиболее ближайшее место, куда мы можем пойти, создана итеративная система поиска ближайшего возможного перемещения.
        /// Значение данной переменной воздействует на количество итераций поиска ближайшего места для перемещения.
        /// </summary>
        private const int CheckDirectionRaycastCount = 10;

        /// <summary>
        /// Количество возможный столкновений.
        /// </summary>
        private const int RaycastCount = 64;
        
        /// <summary>
        /// Максимальная длина raycast'а.
        /// </summary>
        private const float MaxRaycastDistance = 1000f;

        /// <summary>
        /// Текущая гравитация.
        /// </summary>
        public Vector3 GravityVelocity { get; private set; }
        
        /// <summary>
        /// Текущая скорость.
        /// </summary>
        public Vector3 Velocity { get; private set; }
        
        /// <summary>
        /// Объект головы.
        /// </summary>
        public Transform Head;
        
        /// <summary>
        /// Объект рига.
        /// </summary>
        public Transform Rig;
        
        /// <summary>
        /// Высота игрока.
        /// </summary>
        public float Height => Head.transform.localPosition.y + OffsetCameraObject.localPosition.y;

        /// <summary>
        /// Радиус игрока.
        /// </summary>
        public float BodyRadius = 0.15f;

        /// <summary>
        /// Максимальная высота шага.
        /// </summary>
        private float MaximumStepHeight = 0.5f;

        /// <summary>
        /// Маска для игнорирования поиска маршрута.
        /// </summary>
        public LayerMask RaycastIgnoreMask;

        /// <summary>
        /// Список столкновений.
        /// </summary>
        private RaycastHit[] _raycastHits = new RaycastHit[RaycastCount];

        /// <summary>
        /// Совершает ли сейчас прыжок.
        /// </summary>
        private bool _isJumping = false;
        
        /// <summary>
        /// Находится ли он на поверхности.
        /// </summary>
        private bool _isGrounded = false;

        /// <summary>
        /// Позиция рига.
        /// </summary>
        public Vector3 RigPosition => new(Head.transform.position.x, Head.transform.position.y - Head.transform.localPosition.y, Head.transform.position.z);

        /// <summary>
        /// Объект сдвига камеры.
        /// </summary>
        public Transform OffsetCameraObject;

        /// <summary>
        /// Обновление состояния объекта.
        /// </summary>
        private void FixedUpdate()
        {
            if (SceneManager.GetActiveScene().buildIndex != -1)
            {
                return;
            }
            
            if (!ProjectData.IsPlayMode)
            {
                return;
            }
            
            UpdateGravity();

            var targetVelocity = Velocity * Time.fixedDeltaTime;
            var velocity = IsPossibleToMove(targetVelocity) ? targetVelocity : Vector3.zero;
            SetPosition(GetPosition() + velocity + GravityVelocity * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Обновление вектора гравитации.
        /// </summary>
        private void UpdateGravity()
        {
            if (PlayerManager.GravityFreeze || !PlayerManager.UseGravity)
            {
                PlayerManager.FallingTime = 0;
                GravityVelocity = Vector3.zero;
                return;
            }
            
            var ray = new Ray(Head.position, Vector3.down);
            var count = Physics.RaycastNonAlloc(ray, _raycastHits, MaxRaycastDistance, ~RaycastIgnoreMask, QueryTriggerInteraction.Ignore);

            if (count == 0)
            {
                GravityVelocity = Vector3.zero;
                return;
            }

            RaycastHit hit = default;
            var maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                var raycastHit = _raycastHits[i];
                if (IsPartOfGrabbedObject(raycastHit.collider))
                {
                    continue;
                }

                if (!raycastHit.collider.CompareTag("TeleportArea") || raycastHit.point.y < maxY)
                {
                    continue;
                }

                hit = raycastHit;
                maxY = raycastHit.point.y;
            }

            if (hit.Equals(default))
            {
                GravityVelocity = Vector3.zero;
                return;
            }

            var position = GetPosition();
            if (position.y <= hit.point.y && !_isJumping)
            {
                GravityVelocity = Vector3.zero;
                position.y = Mathf.Lerp(position.y, hit.point.y, 16f * Time.fixedDeltaTime);
                SetPosition(position);
                _isGrounded = true;
                PlayerManager.FallingTime = 0;
            }
            else
            {
                GravityVelocity += Physics.gravity * Time.fixedDeltaTime;
                _isGrounded = false;
                PlayerManager.FallingTime += Time.fixedDeltaTime;
            }
            
            _isJumping = false;
        }

        /// <summary>
        /// Проверка возможности пройти.
        /// </summary>
        /// <param name="moveDelta">Дельта перемещения.</param>
        /// <returns>Истина, если проход возможен.</returns>
        private bool IsPossibleToMove(Vector3 moveDelta)
        {
            var capsuleIntersections = BodyCapsuleOverlaps(RigPosition, moveDelta);

            for (int i = 0; i < capsuleIntersections; i++)
            {
                if (IsPartOfGrabbedObject(_raycastHits[i].collider))
                {
                    capsuleIntersections--;
                }
            }
            
            bool hasSideObstacle = capsuleIntersections > 0;
            var beforeJumpFootPosition = Vector3.zero;

            if (!_isGrounded)
            {
                beforeJumpFootPosition = RigPosition;
            }

            const int raycastCount = 10;

            for (var i = 0; i < raycastCount; ++i)
            {
                var t = (float) i / (CheckDirectionRaycastCount - 1);
                var possibleDirection = moveDelta * t;
                var origin = RigPosition + possibleDirection + MaximumStepHeight * Vector3.up;
                var ray = new Ray(origin, Vector3.down);

                var hitCount = Physics.RaycastNonAlloc(ray, _raycastHits, MaxRaycastDistance, ~RaycastIgnoreMask, QueryTriggerInteraction.Ignore);
                if (hitCount == 0)
                {
                    break;
                }
                
                var yMax = float.MinValue;
                RaycastHit hitDown = default;
                bool hasTeleportArea = false;
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    var raycastHit = _raycastHits[hitIndex];
                    if (IsPartOfGrabbedObject(raycastHit.collider))
                    {
                        continue;
                    }

                    if (raycastHit.collider.CompareTag("TeleportArea") && yMax < raycastHit.point.y)
                    {
                        hitDown = raycastHit;
                        yMax = raycastHit.point.y;
                        hasTeleportArea = true;
                    }
                }

                if (!hasTeleportArea)
                {
                    return false;
                }
                
                bool feetAreaFlatEnough = Vector3.Angle(hitDown.normal, Vector3.up) < PlayerManager.TeleportAngleLimit;
                bool canClimb = hitDown.point.y < (RigPosition.y + MaximumStepHeight);

                if (!feetAreaFlatEnough && !_isGrounded)
                {
                    feetAreaFlatEnough = hitDown.point.y < beforeJumpFootPosition.y;
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

        /// <summary>
        /// Каст для проверки столкновения с препятствиями на своем пути.
        /// </summary>
        /// <param name="footPosition">Расположение ног.</param>
        /// <param name="direction">Направление движения.</param>
        /// <returns>Количество столкновений.</returns>
        private int BodyCapsuleOverlaps(Vector3 footPosition, Vector3 direction)
        {
            var downPoint = footPosition + Vector3.up * MaximumStepHeight;
            var upPoint = Head.transform.position;

            return Physics.CapsuleCastNonAlloc(
                upPoint,
                downPoint,
                BodyRadius,
                direction.normalized,
                _raycastHits,
                direction.magnitude,
                ~RaycastIgnoreMask,
                QueryTriggerInteraction.Ignore
            );
        }

        /// <summary>
        /// Является ли объект частью цепочки объектов.
        /// </summary>
        /// <param name="targetCollider">Целевой объект.</param>
        /// <returns>Истина, если является.</returns>
        private bool IsPartOfGrabbedObject(Collider targetCollider)
        {
            if (!targetCollider.attachedRigidbody)
            {
                return false;
            }

            var leftGrabbedObject = InputAdapter.Instance?.PlayerController?.Nodes?.LeftHand?.Controller.GetGrabbedObject();
            var rightGrabbedObject = InputAdapter.Instance?.PlayerController?.Nodes?.RightHand?.Controller.GetGrabbedObject();

            if (leftGrabbedObject && rightGrabbedObject)
            {
                return false;
            }

            var result = false;
            if (leftGrabbedObject)
            {
                result = IsPartOfGrabbedChain(leftGrabbedObject, targetCollider);
            }

            if (rightGrabbedObject)
            {
                result = IsPartOfGrabbedChain(rightGrabbedObject, targetCollider);
            }

            return result;
        }

        /// <summary>
        /// Является ли объект частью грабнутого объекта.
        /// </summary>
        /// <param name="grabbedObject">Взятый объекты.</param>
        /// <param name="targetCollider">Целевой коллайдер.</param>
        /// <returns>Истина, если является.</returns>
        private bool IsPartOfGrabbedChain(GameObject grabbedObject, Collider targetCollider)
        {
            // Если объект взят в руку, то возвращаем истину.
            if (grabbedObject.GetComponent<Rigidbody>() == targetCollider.attachedRigidbody)
            {
                return true;
            }

            var grabbedObjectController = grabbedObject.gameObject.GetWrapper()?.GetObjectController();
            var targetObjectController = targetCollider.gameObject.GetWrapper()?.GetObjectController();
            if (targetObjectController == null || grabbedObjectController == null)
            {
                return false;
            }

            // Если у них одинаковый wrapper, то истина.
            if (grabbedObjectController == targetObjectController)
            {
                return true;
            }

            // Если есть цепочка объектов (сокеты).
            var socketController = grabbedObjectController.gameObject.GetComponent<SocketController>();
            if (socketController)
            {
                var result = false;
                socketController.ConnectionGraphBehaviour.ForEach(a => result |= a.gameObject.GetWrapper().GetObjectController() == targetObjectController);

                if (result)
                {
                    return true;
                }
            }

            // Если объект находится в иерархии и она зафиксирована, то проверяем у детей является ли Rigidbody частью цепочки.
            if (grabbedObjectController.LockChildren && grabbedObjectController.Descendants.Contains(targetObjectController))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Задать позицию рига.
        /// </summary>
        /// <param name="position">Позиция.</param>
        public void SetRigPosition(Vector3 position)
        {
            GravityVelocity = Vector3.zero;
            Velocity = Vector3.zero;
            SetPosition(position);
        }

        /// <summary>
        /// Задать позицию объекта.
        /// </summary>
        /// <param name="position">Позиция.</param>
        private void SetPosition(Vector3 position)
        {
            var playerPosition = position;
            var headDelta = Rig.transform.position - Head.transform.position;

            headDelta.y = 0;
            playerPosition += headDelta;
            Rig.transform.position = playerPosition;
        }

        /// <summary>
        /// Получение позиции объекта.
        /// </summary>
        /// <returns>Позиция объекта.</returns>
        public Vector3 GetPosition()
        {
            return new(Head.position.x, Rig.position.y, Head.position.z);
        }

        /// <summary>
        /// Задать скорость перемещения.
        /// </summary>
        /// <param name="velocity">Скорость.</param>
        public void SetMoveVelocity(Vector3 velocity)
        {
            Velocity = velocity;
        }
        
        /// <summary>
        /// Попробовать прыгнуть.
        /// </summary>
        public void TryJump()
        {
            if (!_isGrounded)
            {
                return;
            }
            
            _isJumping = true;
            GravityVelocity += GetJumpVector();
        }

        /// <summary>
        /// Получение вектора прыжка.
        /// </summary>
        /// <returns>Вектор прыжка.</returns>
        private Vector3 GetJumpVector()
        {
            var jumpHeight = PlayerManager.JumpHeight;
            var origin = Head.position;
            var ray = new Ray(origin, Velocity + Vector3.up * jumpHeight);

            var hitCount = Physics.SphereCastNonAlloc(ray, BodyRadius, _raycastHits, MaxRaycastDistance, ~RaycastIgnoreMask, QueryTriggerInteraction.Ignore);
            if (hitCount == 0)
            {
                return -Physics.gravity * jumpHeight;
            }
            
            for (int i = 0; i < hitCount; i++)
            {
                var raycastHit = _raycastHits[i];
                if (IsPartOfGrabbedObject(raycastHit.collider))
                {
                    continue;
                }

                var distance = raycastHit.distance;
                if (jumpHeight * jumpHeight > distance)
                {
                    jumpHeight = distance;
                }
            }
            
            return -Physics.gravity * jumpHeight;
        }
    }
}