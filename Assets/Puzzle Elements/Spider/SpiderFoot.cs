using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SpiderFoot : MonoBehaviour
{
    [SerializeField] private Transform _placementTarget;
    [SerializeField] private Transform _bodyTransform;
    [SerializeField] private float _stepSize = 1f;
    [SerializeField] private float _footSpeed = 3f;
    [SerializeField] private float _liftHeight = 1.5f;
    [SerializeField] private float _minDistanceTolerance = 0.1f;
    [SerializeField] SpiderFoot _opposingFoot;

    private Vector3 _targetPosition = Vector3.zero;

    public StepPhases _currentPhase = StepPhases.MOVING_TO_TARGET;
    public UnityEvent<bool> OnPlantedChange;
    public enum StepPhases
    {
        RESTING,
        MOVING_TO_TARGET,
        MOVING_TO_LIFT
    }

    private void Start()
    {
        _targetPosition = _placementTarget.position;
    }

    private void Update()
    {
        if(Vector3.Distance(transform.position,_placementTarget.position) > _stepSize && _currentPhase == StepPhases.RESTING && _opposingFoot._currentPhase == StepPhases.RESTING)
        {
            _targetPosition = GetLiftPosition();
            _currentPhase = StepPhases.MOVING_TO_LIFT;
            OnPlantedChange?.Invoke(true);
        }

        if(Vector3.Distance(transform.position,_targetPosition) <_minDistanceTolerance && _currentPhase == StepPhases.MOVING_TO_LIFT)
        {
            _targetPosition = _placementTarget.position;
            _currentPhase = StepPhases.MOVING_TO_TARGET;
        }

        if(Vector3.Distance(transform.position,_targetPosition) < _minDistanceTolerance && _currentPhase == StepPhases.MOVING_TO_TARGET)
        {
            _currentPhase = StepPhases.RESTING;
            OnPlantedChange?.Invoke(true);
        }

        Move();
    }

    private void Move()
    {
        if(_currentPhase != StepPhases.RESTING)
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _footSpeed * Time.deltaTime);
        }
    }
    
    private Vector3 GetLiftPosition()
    {
        Vector3 midPointDistance = (_placementTarget.position - transform.position) / 2;
        Vector3 midPoint = transform.position + midPointDistance;
        Vector3 liftPoint = midPoint + (_bodyTransform.up * _liftHeight);
        return liftPoint;
    }


}
