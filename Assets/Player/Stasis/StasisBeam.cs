using System.Collections;
using Managers.Events;
using UnityEngine;

namespace Player.Stasis
{
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(AudioSource))]
    public class StasisBeam : MonoBehaviour
    {
        [Header("Movimiento")]
        [Tooltip("Velocidad a la que avanza el rayo (unidades/segundo)")]
        [SerializeField] private float beamSpeed = 30f;
        [Tooltip("Tiempo que mantiene encendida la luz tras detenerse")]
        [SerializeField] private float lightOffDelay = .3f;
        [SerializeField] private Light lightStasis;

        [Header("Eventos de Sonido")]
        [Tooltip("Evento que se dispara si no golpea un objeto staseable")]
        public string failEventName    = "StasisFail";
        [Tooltip("Evento que se dispara si golpea un objeto staseable")]
        public string successEventName = "StasisSuccess";

        private Coroutine _beamRoutine;
        
        public void SetBeam(Vector3 start, Vector3 end, bool hit)
        {
            if (_beamRoutine != null) 
                StopCoroutine(_beamRoutine);

            transform.position = start;
            
            Vector3 direction = (end - start).normalized;
            float distance    = Vector3.Distance(start, end);
            if(lightStasis)
                lightStasis.enabled = true;
            
            gameObject.SetActive(true);

            _beamRoutine = StartCoroutine(MoveForward(direction, distance, hit));
        }

        private IEnumerator MoveForward(Vector3 direction, float maxDistance, bool hit)
        {
            float travelled = 0f;

            while (travelled < maxDistance)
            {
                float step = beamSpeed * Time.deltaTime;
                
                if (Physics.Raycast(transform.position, direction, out var info, step))
                {
                    transform.position = info.point;
                    break;
                }
                
                transform.position += direction * step;
                travelled           += step;
                yield return null;
            }
            
            if (lightStasis)
                lightStasis.enabled = false;
            
            
            EventManager.TriggerEvent(hit ? successEventName : failEventName, gameObject);
            
            yield return new WaitForSeconds(lightOffDelay);
            DisableBeam();
        }

        private void DisableBeam()
        {
            Destroy(gameObject);
        }
    }
}
