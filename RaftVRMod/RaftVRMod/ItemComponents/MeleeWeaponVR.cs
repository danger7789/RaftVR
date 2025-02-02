﻿using RaftVR.Utils;
using UnityEngine;

namespace RaftVR.ItemComponents
{
    [RequireComponent(typeof(MeleeWeapon))]
    class MeleeWeaponVR : MonoBehaviour
    {
        MeleeWeapon weapon;
        bool goThroughInvurnability;
        Network_Host hostNetwork;
        Network_Player playerNetwork;
        float damage;
        float cooldown;
        float cooldownTimer;
        CanvasHelper canvas;

        protected virtual void Start()
        {
            weapon = GetComponent<MeleeWeapon>();
            ReflectionInfos.toolOnPressUseEventField.SetValue(weapon, null);
            ReflectionInfos.toolSetAnimationField.SetValue(weapon, false);

            Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;

            playerNetwork = GetComponentInParent<Network_Player>();
            hostNetwork = ComponentManager<Network_Host>.Value;

            damage = (int)ReflectionInfos.weaponDamageField.GetValue(weapon);
            goThroughInvurnability = (bool)ReflectionInfos.weaponGoThroughInvurnabilityField.GetValue(weapon);

            Item_Base item = playerNetwork.PlayerItemManager.useItemController.GetCurrentItemInHand();

            cooldown = item.settings_usable.UseButtonCooldown;
            ReflectionInfos.usableUseAnimationField.SetValue(item.settings_usable, PlayerAnimation.None);

            canvas = ComponentManager<CanvasHelper>.Value;

            // Required for collision detection to work
            gameObject.layer = LayerMask.NameToLayer("Projectiles");
        }

        private void OnEnable()
        {
            cooldownTimer = 0f;
        }

        private void Update()
        {
            if (cooldownTimer > 0)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
                canvas.SetLoadCircle(cooldownTimer / cooldown);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (cooldownTimer > 0 || playerNetwork.PlayerScript.IsDead) return;
            if (weapon.attackMask == (weapon.attackMask | ( 1 << collision.collider.gameObject.layer)))
            {
                Network_Entity_Redirect entityInParent = collision.collider.gameObject.GetComponentInParent<Network_Entity_Redirect>();

                // Raft doesn't have a separate method for this, so I had to borrow from them :P
                if (entityInParent != null)
                {
                    Network_Entity entity = entityInParent.entity;
                    if (entity != null && !entity.IsDead)
                    {
                        weapon.OnMeleeStart();
                        if (goThroughInvurnability && entity.IsInvurnerable)
                        {
                            entity.IsInvurnerable = false;
                        }
                        cooldownTimer = cooldown;
                        hostNetwork.DamageEntity(entity, collision.rigidbody ? collision.rigidbody.transform : collision.collider.transform, damage, collision.contacts[0].point, collision.contacts[0].normal, EntityType.Player, null);
                        if (!entity.IsInvurnerable && entity.removesDurabilityWhenHit)
                        {
                            playerNetwork.Inventory.RemoveDurabillityFromHotSlot(1);
                        }
                    }
                }

                if (weapon is Machete)
                {
                    if (collision.transform.tag == (string)ReflectionInfos.macheteQuestTagField.GetValue(weapon as Machete))
                    {
                        QuestEventBase questEventInparent = collision.collider.transform.GetComponentInParent<QuestEventBase>();
                        if (questEventInparent != null)
                        {
                            cooldownTimer = cooldown;
                            weapon.OnMeleeStart();
                            ReflectionInfos.macheteQuestInteract.Invoke(weapon as Machete, new object[] { questEventInparent });
                        }
                    }
                }
            }
        }
    }
}
