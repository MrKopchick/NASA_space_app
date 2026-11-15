using System.Collections.Generic;
using UnityEngine;

namespace AstraLink.UI
{
    /// <summary>
    /// Controls a single animated Space Center NPC walking randomly across walkable ground tiles.
    /// Uses 4-direction 3-frame sprites (Idle, Step1, Step2) with pendulum animation.
    /// </summary>
    public class SpaceCenterNpcWalker : MonoBehaviour
    {
        private enum MoveDirection
        {
            Left = 0,
            Down = 1,
            Up = 2,
            Right = 3
        }

        private static readonly int[] WalkCyclePattern = { 0, 1, 0, 2 };

        private SpaceCenterNpcManager _manager;
        private SpaceCenterNpcManager.NpcSkin _skin;
        private SpriteRenderer _renderer;

        private readonly Queue<Vector3> _waypoints = new Queue<Vector3>();
        private Vector3 _currentTarget;
        private Vector3 _laneOffset;
        private bool _hasTarget;

        private float _walkSpeed;
        private float _runSpeed;
        private float _currentSpeed;
        private float _idleTimer;
        private float _animTimer;
        private int _animStepIndex;
        private MoveDirection _facing = MoveDirection.Down;
        private int _baseSortingOrder = 3;

        public void Initialize(
            SpaceCenterNpcManager manager,
            SpaceCenterNpcManager.NpcSkin skin,
            Vector3 startPosition,
            int baseSortingOrder,
            Material spriteMaterial)
        {
            _manager = manager;
            _skin = skin;
            _baseSortingOrder = baseSortingOrder;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }
            if (spriteMaterial != null)
            {
                _renderer.sharedMaterial = spriteMaterial;
            }
            _renderer.sortingOrder = _baseSortingOrder;

            _walkSpeed = Random.Range(1.05f, 1.55f);
            _runSpeed = _walkSpeed * Random.Range(1.55f, 1.95f);
            _currentSpeed = _walkSpeed;

            // Keep slight personal lane offset within the tile so NPCs do not stack single-file
            _laneOffset = new Vector3(Random.Range(-0.16f, 0.16f), Random.Range(-0.14f, 0.14f), 0f);
            transform.position = startPosition + _laneOffset;

            _facing = (MoveDirection)Random.Range(0, 4);
            _idleTimer = Random.Range(0.15f, 1.4f);
            _hasTarget = false;
            _waypoints.Clear();

            ApplySprite(_facing, 0);
            UpdateSortingOrder();
        }

        private void Update()
        {
            if (_manager == null || _skin == null || _renderer == null) return;
            if (SpaceCenterMapUI.Instance != null && !SpaceCenterMapUI.Instance.IsMapOpen) return;

            if (_hasTarget)
            {
                MoveAlongPath();
            }
            else
            {
                UpdateIdleState();
            }

            UpdateSortingOrder();
        }

        private void UpdateIdleState()
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer > 0f) return;

            if (_manager.TryBuildRandomPath(transform.position, out Queue<Vector3> newPath) && newPath.Count > 0)
            {
                _waypoints.Clear();
                while (newPath.Count > 0)
                {
                    _waypoints.Enqueue(newPath.Dequeue());
                }

                // 25% chance to briskly jog across the spaceport
                _currentSpeed = Random.value < 0.25f ? _runSpeed : _walkSpeed;
                AdvanceToNextWaypoint();
            }
            else
            {
                _idleTimer = Random.Range(0.8f, 1.8f);
            }
        }

        private void MoveAlongPath()
        {
            Vector3 currentPos = transform.position;
            Vector3 toTarget = _currentTarget - currentPos;
            float dist = toTarget.magnitude;

            if (dist <= 0.03f)
            {
                transform.position = _currentTarget;
                if (!AdvanceToNextWaypoint())
                {
                    _hasTarget = false;
                    _idleTimer = Random.Range(0.7f, 2.6f);
                    // Occasionally glance in another direction while standing
                    if (Random.value < 0.35f)
                    {
                        _facing = (MoveDirection)Random.Range(0, 4);
                    }
                    _animStepIndex = 0;
                    ApplySprite(_facing, 0);
                }
                return;
            }

            UpdateFacing(toTarget);
            float step = _currentSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(currentPos, _currentTarget, step);

            float frameInterval = _currentSpeed > _walkSpeed * 1.25f ? 0.10f : 0.16f;
            _animTimer += Time.deltaTime;
            if (_animTimer >= frameInterval)
            {
                _animTimer = 0f;
                _animStepIndex = (_animStepIndex + 1) % WalkCyclePattern.Length;
                ApplySprite(_facing, WalkCyclePattern[_animStepIndex]);
            }
        }

        private bool AdvanceToNextWaypoint()
        {
            if (_waypoints.Count == 0)
            {
                _hasTarget = false;
                return false;
            }

            Vector3 cellCenter = _waypoints.Dequeue();
            _currentTarget = cellCenter + _laneOffset;
            _currentTarget.z = 0f;
            _hasTarget = true;
            return true;
        }

        private void UpdateFacing(Vector3 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                _facing = delta.x >= 0f ? MoveDirection.Right : MoveDirection.Left;
            }
            else
            {
                _facing = delta.y >= 0f ? MoveDirection.Up : MoveDirection.Down;
            }
        }

        private void ApplySprite(MoveDirection dir, int frameIndex)
        {
            Sprite[] frames = null;
            switch (dir)
            {
                case MoveDirection.Left: frames = _skin.Left; break;
                case MoveDirection.Down: frames = _skin.Down; break;
                case MoveDirection.Up: frames = _skin.Up; break;
                case MoveDirection.Right: frames = _skin.Right; break;
            }

            if (frames == null || frames.Length == 0) return;
            int safeIdx = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            Sprite s = frames[safeIdx] ?? frames[0];
            if (s != null && _renderer.sprite != s)
            {
                _renderer.sprite = s;
            }
        }

        private void UpdateSortingOrder()
        {
            int yOffsetOrder = Mathf.Clamp(Mathf.RoundToInt((20f - transform.position.y) * 10f), 0, 400);
            _renderer.sortingOrder = _baseSortingOrder + yOffsetOrder;
        }
    }
}
