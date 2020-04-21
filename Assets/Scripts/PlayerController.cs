﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

public class PlayerController : Controller
{
    Rigidbody2D rb = null;

    float axisMovementDeadzone = 0.01f;

    protected new void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;
    }

    protected new void Update()
    {
        base.Update();
    }

    private void FixedUpdate()
    {
        HandleInput();
    }

    void HandleInput()
    {
        float horizontalAxis = Input.GetAxis("Horizontal_" + teamNumber);
        float verticalAxis = Input.GetAxis("Vertical_" + teamNumber);

        if (Mathf.Abs(horizontalAxis) > axisMovementDeadzone || Mathf.Abs(verticalAxis) > axisMovementDeadzone)
        {
            Move(new Vector2(horizontalAxis, verticalAxis));
        }

        if(Input.GetButtonDown("Fire_" + teamNumber))
        {
            if (!TryPickup())
            {
                if (HasAmmo())
                {
                    TryAttack();
                }
            }
        }
    }

    void Move(Vector2 movement)
    {
        Vector2 targetPosition = new Vector2(transform.position.x, transform.position.y) + movement * movementSpeed * Time.deltaTime;
        rb.MovePosition(targetPosition);

        float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
        Quaternion quart = Quaternion.AngleAxis(angle, Vector3.forward);

        transform.rotation = Quaternion.RotateTowards(transform.rotation, quart, Time.deltaTime * rotationSpeed);
    }

    bool TryAttack()
    {
        Controller attackee = null;

        foreach (Controller enemy in World.agents)
        {
            if (enemy == null || enemy.teamNumber == teamNumber) continue;

            if (Vector3.Distance(enemy.transform.position, transform.position) > attackRange) continue;
   
            Vector3 enemyDir = Vector3.Normalize(enemy.transform.position - transform.position);
            float angle = Vector3.Angle(transform.right, enemyDir);
            if (angle > attackAngle * 0.5f) continue;

            attackee = enemy;

            break;
        }

        return Attack(attackee);
    }


    bool TryPickup()
    {
        return (TryPickupAmmo() || TryPickupHealth());
    }

    bool TryPickupAmmo()
    {
        if (!HasFullAmmo())
        {
            foreach (AmmoTile ammoTile in World.ammoTiles)
            {
                if (TryPickupBase(ammoTile))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool TryPickupHealth()
    {
        if (!IsFullHealth())
        {
            foreach (HealthTile healthTile in World.healthTiles)
            {
                if (TryPickupBase(healthTile))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool TryPickupBase(PickupTile pickupTile)
    {
        if (Vector3.Distance(pickupTile.mTileObject.transform.position, transform.position) < pickupRange)
        {
            return pickupTile.Pickup(this);
        }

        return false;
    }
}
