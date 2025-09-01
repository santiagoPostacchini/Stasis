using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("<color=orange>Animator Override Controller</color>")]
    [SerializeField] private AnimatorOverrideController _overrideController;

    [Header("<color=yellow>Bools</color>")]
    [SerializeField] public bool _attackAnimationsActive = true;  
    [SerializeField] public bool IsAttacking = false; 
    [SerializeField] public bool _canAttack = true; 
    [SerializeField] public bool IsAttackingNow = false; 


    [Header("<color=red>Dependencies</color>")]
    [SerializeField] public Animator _armsAnimator;

    [Header("<color=purple>Attack Settings</color>")]
    [SerializeField] public KeyCode _attackKey = KeyCode.Mouse0;
    [SerializeField] private float _attackCoolDown = 0.1f;
    [SerializeField] private float _attackThreshold = 1.2f;
    [SerializeField] private float _diferentAttackThreshold = 3f;

    [Header("<color=green>Input Settings</color>")]
    [SerializeField, Range(0f, 0.5f)] private float _maxPressDuration = 0.5f;

    private float lastClickedTime;
    private float lastComboEnd;
    private int comboCounter;
    private float keyPressStartTime;

    void Start()
    {

    }

    void Update()
    {
        if (!_attackAnimationsActive) return; 

        if (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.01f && _armsAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            IsAttacking = true;
        }

        if (Input.GetKeyDown(_attackKey))
        {
            keyPressStartTime = Time.time;
        }

        if (Input.GetKeyUp(_attackKey) && _canAttack)
        {
            float keyPressDuration = Time.time - keyPressStartTime;

            if (keyPressDuration >= 0f && keyPressDuration <= _maxPressDuration)
            {
                Attack();
            }
        }

        ExitAttack();
    }

    void Attack()
    {
        if (Time.time - lastComboEnd > _attackCoolDown)
        {
            CancelInvoke("EndCombo");

            if (Time.time - lastClickedTime >= _attackThreshold)
            {
                switch (comboCounter)
                {
                    case 0:
                        _armsAnimator.Play("Player_Arm_Attack_1");
                        break;
                    case 1:
                        _armsAnimator.Play("Player_Arm_Attack_2");
                        break;
                    case 2:
                        _armsAnimator.Play("Player_Arm_Attack_3");
                        break;
                }

                comboCounter++;
                IsAttacking = true;
                lastClickedTime = Time.time;
                IsAttackingNow = true;

                if (comboCounter >= 3)
                {
                    comboCounter = 0;
                }
            }
        }
    }

    void ExitAttack()
    {
        if (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f && _armsAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            IsAttackingNow = false;
        }


        if (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f && _armsAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            IsAttacking = false;
        }

        if (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > _diferentAttackThreshold && _armsAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack"))
        {
            Invoke("EndCombo", 1);
        }
    }

    void EndCombo()
    {
        comboCounter = 0;
        lastComboEnd = Time.time;
    }

    public bool AttackAnimationsActive
    {
        get { return _attackAnimationsActive; }
        set { _attackAnimationsActive = value; }
    }
}