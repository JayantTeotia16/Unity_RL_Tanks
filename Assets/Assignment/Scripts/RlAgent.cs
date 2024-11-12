using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

/// <summary>
/// Tanks RL Agent
/// </summary>
public class RlAgent : Agent
{
    [Tooltip("Prefab")] 
    public Rigidbody shell;   
    
    private Rigidbody rBody;
    private float reward = 0f;
    private Transform tankTurret;

    private TankNewSpawn tankNewSpawn;

    private TankFiringSystem tankFiring;

    private float fireKeyDown = 0f;

    public float ifShoot = -1f;

    public float maxRotationAngle = 60f;  

    private float initialTurretRotationY;

    /// <summary>
    /// Initialize the agent
    /// </summary>
    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
        tankTurret = transform.Find("TankRenderers/TankTurret");
        initialTurretRotationY = tankTurret.eulerAngles.y;
        tankNewSpawn = GetComponentInParent<TankNewSpawn>();
        tankFiring = rBody.GetComponent<TankFiringSystem>();
    }

    /// <summary>
    /// Reset the Agent when episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        rBody.velocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;
        transform.localPosition = new Vector3(0f, 0f, -35f);
        tankTurret.rotation = Quaternion.identity;
        ifShoot = 0f;
        fireKeyDown = 0f;
        reward = 0f;
        shell = FireInput();


    }

    public override void CollectObservations(VectorSensor sensor)
    {
       sensor.AddObservation(rBody.position.x);
       foreach (Transform child in tankNewSpawn.transform)
       {
            if (child.CompareTag("EnemyAI"))
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(child.position - rBody.position);
                break;
            }
            else if (child.CompareTag("Friendly"))
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(child.position - rBody.position);
                break;
            }
            
       }
    
        
    }

    /// <summary>
    /// Called when an action is recieved from a neural network
    /// </summary>
    /// <param name="actionBuffers">The actions to take</param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        Vector3 controlSignal = Vector3.zero;
        controlSignal.x = actionBuffers.ContinuousActions[0]/2;
        rBody.MovePosition(rBody.transform.position + controlSignal);

        float currentRotation = tankTurret.eulerAngles.y;
        float turPos = actionBuffers.ContinuousActions[2]*45;
        float targetRotation = currentRotation + turPos;
        float clampedRotation = 0f;
        if (targetRotation<180)
        {
            clampedRotation = Mathf.Clamp(targetRotation, initialTurretRotationY - maxRotationAngle, initialTurretRotationY + maxRotationAngle);

        }
        else
        {
            clampedRotation = Mathf.Clamp(targetRotation, 360 - maxRotationAngle, 360);
        }
        tankTurret.rotation = Quaternion.Euler(tankTurret.eulerAngles.x, clampedRotation, tankTurret.eulerAngles.z);

        ifShoot = actionBuffers.ContinuousActions[1];
        if (shell == null)
        {
            if (ifShoot<=0)
            {
                ifShoot = 0f;
            }

            ifShoot = Mathf.Lerp(0f, 30f, ifShoot);
        }
        
        if (ifShoot > 5f )
        {
            fireKeyDown=ifShoot;
        }
        else
        {
            fireKeyDown=0f;
        }
        FireInput();
        if (shell != null)
        {
            if (shell.transform.position.y < 0f)
            {
                
                Destroy(shell.gameObject);
            }
        }
        
        
        foreach (Transform child in tankNewSpawn.transform)
        {
            
            if (shell != null)
            {
                Vector3 explosionToTarget = child.position - shell.transform.position;
                float explosionDistance = explosionToTarget.magnitude;
                if (explosionDistance < 2f)
                {
                    if (child.CompareTag("EnemyAI"))
                    {
                        tankNewSpawn.DestroyOneTank(child);
                        reward += 2f;
                        AddReward(+2f);
                    }
                    else if ( child.CompareTag("Friendly"))
                    {
                        tankNewSpawn.DestroyOneTank(child);
                        reward -= 1f;
                        AddReward(-1f);
                    }
                }
            }
            
            if ((child.position - rBody.position).magnitude < 2.0f && child.CompareTag("EnemyAI"))
            {
                
                tankNewSpawn.DestroyAllTanks();

                AddReward(-5f);
                if (shell != null)
                {
                    Destroy(shell.gameObject);
                }
                EndEpisode();
            }
            if (child.position.z < -36f)
            {
                if (child.CompareTag("EnemyAI"))
                {
                    tankNewSpawn.DestroyOneTank(child);
                    reward -= 1f;
                    AddReward(-1f);
                }
                else if ( child.CompareTag("Friendly"))
                {
                    tankNewSpawn.DestroyOneTank(child);
                    reward += 2f;
                    AddReward(+2f);
                }
            }
            
        }

        

        if (transform.position.x < -37f || transform.position.x > 37f)
        {
            tankNewSpawn.DestroyAllTanks();
            AddReward(-10f);
            if (shell != null)
            {
                Destroy(shell.gameObject);
            }
            EndEpisode();
        }
        else if (reward >= 20)
        {
            tankNewSpawn.DestroyAllTanks();
            if (shell != null)
            {
                Destroy(shell.gameObject);
            }
            EndEpisode();
        }
        else
        {
            AddReward(-0.0001f);
        }
    }

    /// <summary>
    /// Heuristic Function
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
        continuousActionsOut[2] = Input.GetKey(KeyCode.Space) ? -1.0f : 0.0f;

        
    }

    /// <summary>
    /// Fire function
    /// </summary>
    /// <returns></returns>
    public Rigidbody FireInput()
    {
        if (fireKeyDown != 0f)
        {
            Vector3 turretForward = tankTurret.forward;
            shell = tankFiring.Fire(turretForward,fireKeyDown);

            fireKeyDown = 0f;
            
        }
        return null;
    }

    /// <summary>
    /// Function to rotate the turret
    /// </summary>
    /// <param name="angle"></param>
    public void RotateTurret(float angle)
{
    // Logic for rotating the turret
    tankTurret.Rotate(Vector3.up, angle);
}

}