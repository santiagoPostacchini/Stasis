using UnityEngine;
using System;

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
        public float TurningSpeed = 10f;

        [Tooltip("Si está activado, el objeto recorrerá el camino en un bucle infinito. Si está desactivado, hará un recorrido de ida y vuelta.")]
        public bool IsLoop = false;

        [Tooltip("Si está activado, el objeto se moverá a lo largo del camino. Si está desactivado, permanecerá quieto.")]
        public bool IsMove = true;

        [Tooltip("Si está activado, se invocará el evento EndEvent cuando el objeto llegue al final del recorrido.")]
        public bool IsEndEventEnable = false;
        private bool checkFlag = false;
        protected Rigidbody targetRigidbody;
        protected GameObject target;
        [HideInInspector]public Vector3 nextPath;
        [HideInInspector]public int pathIndex = 1;
        private bool isForward = true; // true = avanzando, false = retrocediendo
        private void Start()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            particle = GetComponentInChildren<ParticleSystem>();

            if (Generator != null)
            {
                target = this.gameObject;
                nextPath = Generator.PathList[1];
                this.transform.position = Generator.PathList[0];
            }
        }

        public void FixedUpdate()
        {
            if (!IsMove)
            {
                targetRigidbody.velocity = Vector3.zero;
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

            if (!checkFlag)
            {
                checkFlag = true;
                target = this.gameObject;
                nextPath = Generator.PathList[1];
                this.transform.position = Generator.PathList[0];
            }

            // Look at next path
            Vector3 offset = nextPath - target.transform.position;
            offset.Normalize();
            Quaternion q = Quaternion.LookRotation(offset);
           // targetRigidbody.rotation = Quaternion.Slerp(targetRigidbody.rotation, q, TurningSpeed * Time.deltaTime);

            // Move towards next path
            targetRigidbody.velocity = Speed * Time.deltaTime * offset;

            float distance = Vector3.Distance(nextPath, target.transform.position);

            if (distance < DistanceThreshold)
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
                            nextPath = Generator.PathList[pathIndex];
                            if (EndEvent != null && IsEndEventEnable)
                                EndEvent.Invoke();
                        }
                    }
                }
                else
                {
                    // --- LOOP NORMAL ---
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
    }
}
