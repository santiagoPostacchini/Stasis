using UnityEngine;
using System.Collections;

namespace CurvedPathGenerator
{
    [RequireComponent(typeof(Rigidbody))]
    public class PathFollower1 : MonoBehaviour
    {
        [Tooltip("Partículas que se reproducen mientras el objeto se mueve (opcional).")]
        public ParticleSystem particle;

        [Tooltip("Evento que se invoca cuando el objeto llega al final del recorrido (opcional).")]
        public UnityEngine.Events.UnityEvent EndEvent;

        [Tooltip("Referencia al generador de caminos que define los puntos por los que se moverá el objeto.")]
        public PathGenerator Generator;

        [Tooltip("Velocidad a la que se mueve el objeto a lo largo del camino.")]
        public float Speed = 100f;

        [Tooltip("Distancia mínima al siguiente punto para considerarlo alcanzado.")]
        public float DistanceThreshold = 0.2f;

        [Tooltip("Velocidad con la que el objeto rota para mirar hacia el siguiente punto.")]
        public float TurningSpeed = 10f; // NO la usamos (respetamos lo que pediste: no rotar)

        [Tooltip("Si está activado, el objeto recorrerá el camino en un bucle infinito. Si está desactivado, hará un recorrido de ida y vuelta.")]
        public bool IsLoop = false;

        [Tooltip("Si está activado, el objeto se moverá a lo largo del camino. Si está desactivado, permanecerá quieto.")]
        public bool IsMove = true;

        [Tooltip("Si está activado, se invocará el evento EndEvent cuando el objeto llegue al final del recorrido.")]
        public bool IsEndEventEnable = false;

        private bool checkFlag = false;
        protected Rigidbody targetRigidbody;
        protected GameObject target;
        [HideInInspector] public Vector3 nextPath;
        [HideInInspector] public int pathIndex = 1;
        private bool isForward = true;       // true = avanzando, false = retrocediendo
        private bool lastIsForward = true;
        private bool canMove = true;

        // >>> Nuevo: cache para detectar si el generador (o su padre) cambió de TRS
        private Matrix4x4 _lastGenMatrix;

        private void Start()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            particle = GetComponentInChildren<ParticleSystem>();

            if (Generator != null)
            {
                // Asegurar path válido antes de acceder
                EnsurePathUpToDate();

                if (Generator.PathList != null && Generator.PathList.Count >= 2)
                {
                    target = this.gameObject;
                    pathIndex = Mathf.Clamp(pathIndex, 1, Generator.PathList.Count - 1);
                    nextPath = Generator.PathList[pathIndex];
                    this.transform.position = Generator.PathList[0];
                    _lastGenMatrix = Generator.transform.localToWorldMatrix;
                    checkFlag = true;
                }
                else
                {
                    Debug.LogError($"{name} PathFollower1: PathList vacío o con menos de 2 puntos.");
                    IsMove = false;
                }
            }
        }

        public void FixedUpdate()
        {
            if (!IsMove)
            {
                if (targetRigidbody != null) targetRigidbody.velocity = Vector3.zero;
                return;
            }
            else
            {
                if (particle != null && !particle.isPlaying)
                    particle.Play();
            }

            if (Generator == null)
            {
                IsMove = false;
                checkFlag = false;
                Debug.LogError("No path assigned");
                return;
            }

            // Refrescar curva si cambió el TRS del generador (o si éste recalcula en vivo)
            RefreshPathIfNeeded();

            // Init seguro la primera vez (o tras refresco inicial)
            if (!checkFlag)
            {
                if (Generator.PathList == null || Generator.PathList.Count < 2)
                {
                    Debug.LogError($"{name} PathFollower1: PathList inválido.");
                    IsMove = false;
                    return;
                }
                checkFlag = true;
                target = this.gameObject;
                pathIndex = Mathf.Clamp(pathIndex, 1, Generator.PathList.Count - 1);
                nextPath = Generator.PathList[pathIndex];
                this.transform.position = Generator.PathList[0];
            }

            if (DidDirectionChange())
            {
                canMove = false;
            }

            // Movimiento hacia el próximo punto (SIN ROTAR)
            Vector3 offset = nextPath - target.transform.position;
            float dist = offset.magnitude;

            if (dist > 1e-6f)
            {
                Vector3 dir = offset / dist;

                // NO rotamos (respetamos tu pedido)
                // Quaternion q = Quaternion.LookRotation(dir);
                // targetRigidbody.rotation = Quaternion.Slerp(targetRigidbody.rotation, q, TurningSpeed * Time.deltaTime);

                if (canMove)
                {
                    Vector3 step = dir * Speed * Time.fixedDeltaTime;
                    // Evitar overshoot si el frame “salta” mucho
                    if (step.magnitude > dist) step = dir * dist;

                    targetRigidbody.MovePosition(targetRigidbody.position + step);
                }
            }

            // ¿Llegó?
            if (dist < DistanceThreshold)
            {
                if (!IsLoop && !Generator.IsClosed)
                {
                    // --- IDA Y VUELTA ---
                    if (isForward)
                    {
                        if (pathIndex + 1 < Generator.PathList.Count)
                        {
                            nextPath = Generator.PathList[++pathIndex];
                        }
                        else
                        {
                            isForward = false;
                            pathIndex--;
                            pathIndex = Mathf.Clamp(pathIndex, 0, Generator.PathList.Count - 1);
                            nextPath = Generator.PathList[pathIndex];
                            if (EndEvent != null && IsEndEventEnable)
                                EndEvent.Invoke();
                        }
                    }
                    else // retrocediendo
                    {
                        if (pathIndex - 1 >= 0)
                        {
                            nextPath = Generator.PathList[--pathIndex];
                        }
                        else
                        {
                            isForward = true;
                            pathIndex++;
                            pathIndex = Mathf.Clamp(pathIndex, 0, Generator.PathList.Count - 1);
                            nextPath = Generator.PathList[pathIndex];
                            if (EndEvent != null && IsEndEventEnable)
                                EndEvent.Invoke();
                        }
                    }
                }
                else
                {
                    // --- LOOP NORMAL / PATH CERRADO ---
                    if (pathIndex + 1 < Generator.PathList.Count)
                    {
                        nextPath = Generator.PathList[++pathIndex];
                    }
                    else
                    {
                        if (Generator.IsClosed || IsLoop)
                        {
                            if (EndEvent != null && IsEndEventEnable)
                                EndEvent.Invoke();

                            if (IsLoop)
                            {
                                nextPath = Generator.PathList[0];
                                pathIndex = 0;
                            }
                            else
                            {
                                StopFollow();
                            }
                        }
                        else
                        {
                            StopFollow();
                            if (EndEvent != null && IsEndEventEnable)
                                EndEvent.Invoke();
                        }
                    }
                }
            }
        }

        // Detecta cambio de sentido para pausar (tu lógica original)
        public bool DidDirectionChange()
        {
            if (isForward != lastIsForward)
            {
                lastIsForward = isForward;
                canMove = false;
                StartCoroutine(Wait());
                return true;
            }
            return false;
        }

        IEnumerator Wait()
        {
            targetRigidbody.isKinematic = false;
            targetRigidbody.velocity = Vector3.zero;
            yield return new WaitForSeconds(1f);
            targetRigidbody.isKinematic = true;
            canMove = true;
        }

        public float GetPassedLength()
        {
            if (Generator == null) return -1;

            if (pathIndex == 1)
                return (Generator.PathList[0] - this.transform.position).magnitude;
            else if (pathIndex >= Generator.PathList.Count)
                return Generator.GetLength();
            else
                return Generator.PathLengths[pathIndex - 2] + (Generator.PathList[pathIndex - 1] - this.transform.position).magnitude;
        }

        public void StopFollow()
        {
            IsMove = false;
            if (particle != null)
                particle.Stop();
        }

        public void StartFollow()
        {
            if (Generator == null) return;
            IsMove = true;
        }

        // =================== Helpers nuevos ===================

        // Si el PathGenerator es hijo de algo que se mueve, su matriz cambia:
        // refrescamos la curva y mantenemos el pathIndex/nextPath coherentes en mundo.
        private void RefreshPathIfNeeded()
        {
            if (Generator == null) return;

            bool trsChanged = (_lastGenMatrix != Generator.transform.localToWorldMatrix);
            if (trsChanged || Generator.IsLivePath)
            {
                EnsurePathUpToDate();

                // Revalidar índices y actualizar el nextPath según el índice actual
                if (Generator.PathList != null && Generator.PathList.Count >= 2)
                {
                    pathIndex = Mathf.Clamp(pathIndex, 1, Generator.PathList.Count - 1);
                    nextPath = Generator.PathList[pathIndex];
                }

                _lastGenMatrix = Generator.transform.localToWorldMatrix;
            }
        }

        private void EnsurePathUpToDate()
        {
            // Recalcula la curva con las posiciones en mundo correctas
            Generator.UpdatePath();
        }
    }
}
