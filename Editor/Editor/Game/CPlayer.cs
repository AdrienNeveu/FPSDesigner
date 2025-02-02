﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Engine.Game
{
    class CPlayer
    {
        public CEnemy botController;
        public string userName;
        public int ID;
        public int gunId = 0;

        private Vector3 newPos;
        private Vector3 oldPos;
        private DateTime oldTime;
        private Vector3 direction;

        public float life;

        private bool isCrouched = false;

        public CPlayer(int PlayerID, string Name, Vector3 pos)
        {
            this.userName = Name;
            this.ID = PlayerID;

            Texture2D[] textures = new Texture2D[1];
            textures[0] = Display2D.C2DEffect._content.Load<Texture2D>("Textures\\MedievalCharacter");
            botController = new CEnemy("MedievalCharacter", textures, pos, Matrix.CreateFromYawPitchRoll(0, 0, 0), 100f, 20, 10, false, Name, 2);
            botController._isMultiPlayer = true;
            Game.CEnemyManager.AddEnemy(Display2D.C2DEffect._content, CConsole._Camera, botController);

            oldPos = pos;
            newPos = pos;
            oldTime = DateTime.Now;

            life = 100;
            botController._life = life;
        }

        public void SetNewPos(Vector3 pos, Vector3 rot)
        {
            oldPos = botController._position;
            newPos = pos;
            direction = newPos - oldPos;
            botController._multiSpeed = (float)((direction.Length()) / ((DateTime.Now - oldTime).TotalMilliseconds));
            direction.Normalize();
            oldTime = DateTime.Now;

            botController._multiDirection = direction;

            botController._position = pos;
            botController.rotationValue = rot.X;
        }

        public void Disconnect()
        {
            Game.CEnemyManager.RemoveBot(botController);
        }

        public void ReceiveHit(float damage, string hitbox)
        {
            botController.HandleDamages(damage, hitbox);
            life = botController._life;
        }

        public void SetWalk(bool toggle)
        {
            botController.SetWalk(toggle, CConsole._Weapon._weaponsArray[gunId].MultiType);
        }

        public void SetCrouched(bool toggle)
        {
            isCrouched = toggle;
            botController.Crouch(toggle, CConsole._Weapon._weaponsArray[gunId].MultiType);
        }

        public void SetJump(bool toggle)
        {
            if (toggle)
                botController.SetJump(CConsole._Weapon._weaponsArray[gunId].MultiType);
        }

        public void SetReload(bool toggle)
        {
            if (toggle)
                botController.SetReload(CConsole._Weapon._weaponsArray[gunId].MultiType);
        }

        public void SetShot(bool toggle)
        {
            if (toggle)
            {
                botController.SetShot(CConsole._Weapon._weaponsArray[gunId].MultiType);
                botController.PlayFireSound(CConsole._Camera._cameraPos);
            }
        }


        public void SetSwitch(bool toggle)
        {
            if (toggle)
            {
                botController.SetSwitchingWeapon(CConsole._Weapon._weaponsArray[gunId].MultiType);
                botController.ChangeWeapon(gunId);
            }
        }

        public string CheckIntersects(Ray ray, out float? distance)
        {
            return botController.RayIntersectsHitbox(ray, out distance);
        }

        public float GetRealDamages(string hitbox)
        {
            return botController.GetDamages(CConsole._Weapon._weaponsArray[gunId]._damagesPerBullet, hitbox);
        }
    }
}
