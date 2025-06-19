using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Player.Stasis;
using Player.Scripts.Interactor;

public class StasisGunEntity : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Transform stasisOrigin;
    [SerializeField] private GameObject stasisBeamPrefab;
    [SerializeField] private float beamDuration = 0.2f;

    private GameObject _firstFrozenObject;
    private IStasis _firstStasisComponent;

    private GameObject _secondFrozenObject;
    private IStasis _secondStasisComponent;

    private StasisBeam _activeBeam;
    private Coroutine _beamCoroutine;

    private PlayerInteractor _playerInteractor;

    private bool IsGunActive => this.enabled && this.gameObject.activeInHierarchy;

    //public GameObject target;
    public List<GameObject> targets = new List<GameObject>();

    void Start()
    {
        _playerInteractor = GetComponent<PlayerInteractor>();
    }



    
    public IEnumerator waiTryStasis(Transform end, DestroyedPieceController part)
    {
        yield return new WaitForSeconds(0.5f);
        TryStasis(end, part);
    }
    public void TryStasis(Transform end, DestroyedPieceController part)
    {
        Vector3 direction = (end.position - transform.position).normalized;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, Mathf.Infinity))
        {

            bool stasisHit = false;
            GameObject hitObject = hit.collider.gameObject;
            if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
            {

                stasisComponent.StatisEffectActivate();
                //ApplyStasisEffect(hitObject, stasisComponent);
                stasisHit = true;
            }


            GameObject beamInstance = Instantiate(stasisBeamPrefab, stasisOrigin.position, Quaternion.LookRotation(direction)  // Rotación que apunta hacia la dirección del rayo
  );
            beamInstance.transform.localScale *= 2f;

            _activeBeam = beamInstance.GetComponent<StasisBeam>();
            _activeBeam.SetBeam(stasisOrigin.position, hit.point, stasisHit);

            // AudioManager.Instance?.PlaySfx("LaserFX");

            if (_beamCoroutine != null)
                StopCoroutine(_beamCoroutine);

            _beamCoroutine = StartCoroutine(DisableBeamAfterDuration(beamDuration));
        }
    }
   
    private IEnumerator DisableBeamAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_activeBeam)
        {
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }
    }

    //private void OnDisable()
    //{
    //    UnfreezeAllObjects();
    //}

    private void UnfreezeAllObjects()
    {
        _firstStasisComponent?.StatisEffectDeactivate();
        _secondStasisComponent?.StatisEffectDeactivate();

        _firstFrozenObject = null;
        _secondFrozenObject = null;
        _firstStasisComponent = null;
        _secondStasisComponent = null;
    }
}