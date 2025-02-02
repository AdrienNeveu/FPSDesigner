﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

// TYPE 0 : Gun, Semi Auto, Auto...
// TYPE 1 : Sniper
// TYPE 2 : Cut

namespace Engine.Game
{
    class CWeapon
    {
        private int _weaponsAmount;
        private double _lastShotMs;
        public int _selectedWeapon;
        private bool _dryFirePlayed;

        public List<WeaponData> _weaponsArray; // All weapons
        public List<WeaponData> _weaponPossessed; // Weapons Possessed by the player

        #region "WeaponData Class"
        public class WeaponData
        {
            #region "Constructor"
            public WeaponData(Model weaponModel, object[] weaponInfo, string[] weaponsSound, string[] weapAnim, float[] animVelocity, Texture2D weapTexture)
            {
                // Model Assignement
                this._wepModel = weaponModel;

                // Integers Assignement
                this._wepType = (int)weaponInfo[0];
                this._actualClip = (int)weaponInfo[1];
                this._maxClip = (int)weaponInfo[2];
                this._bulletsAvailable = (int)weaponInfo[3];
                this._magazinesAvailables = (int)weaponInfo[4];
                this._isAutomatic = (bool)weaponInfo[5];
                this._shotPerSeconds = (int)(1000.0f / (float)weaponInfo[6]);
                this._range = (int)weaponInfo[7];

                this._rotation = (Matrix)weaponInfo[8];
                this._offset = (Vector3)weaponInfo[9];
                this._scale = (float)weaponInfo[10];
                
                this._delay = (float)weaponInfo[11];

                this._name = (string)weaponInfo[12];

                this._recoilIntensity = (float)weaponInfo[13];
                this._recoilBackIntensity = (float)weaponInfo[14];

                this._damagesPerBullet = (float)weaponInfo[15];

                this._animVelocity = animVelocity;

                this._weapTexture = weapTexture;

                // SoundEffect Assignement
                this._shotSound = weaponsSound[0];
                if (_wepType != 2)
                {
                    this._dryShotSound = weaponsSound[1];
                    this._reloadSound = weaponsSound[2];
                }

                //Anim
                this._weapAnim = weapAnim;
            }
            #endregion

            /* WepType:
             * 0: HandGun
             * 1: Static weapons (tripod, mortars, etc.)
             * 2: Knife
             */
            public int _wepType;

            // Clips & Magazines
            public int _actualClip; // Bullets available in one magazine
            public int _maxClip; //Max bullets per magazine
            public int _bulletsAvailable; // Bullets left
            public int _magazinesAvailables;
            public float _recoilIntensity;
            public float _recoilBackIntensity;

            // Other Weapons Infos
            public bool _isAutomatic;
            public int _shotPerSeconds; // 0 if non automatic
            public float _damagesPerBullet; // Each bullet gives some damages
            public int _range; // 0 if unlimited range
            public float _delay; // the delay used to play the sound
            public string _name; // The weapon name

            // Models
            public Model _wepModel;

            // The baked texture
            public Texture2D _weapTexture;

            // Anim
            public String[] _weapAnim;

            // Velocity Anim
            public float[] _animVelocity;

            // Sounds
            public string _shotSound;
            public string _dryShotSound;
            public string _reloadSound;

            // Display
            public Matrix _rotation;
            public Vector3 _offset;
            public float _scale;

            public string MultiType
            {
                get
                {
                    switch (_name)
                    {
                        default:
                        case "Machete":
                            return "machete";
                        case "M1911":
                        case "Deagle":
                            return "handgun";
                        case "AK47":
                        case "M40A5":
                            return "heavy";
                        case "Bow":
                            return "bow";
                    }
                }
            }
        }
        #endregion

        public CWeapon()
        {

        }

        public void LoadContent(ContentManager content, Dictionary<string, Model> modelsList, Dictionary<string, Texture2D> weapTexture, List<object[]> weaponsInfo, List<string[]> weaponsSounds, List<string[]> weapAnim,
            List<float[]> animVelocity)
        {
            if ((modelsList.Count != weaponsInfo.Count || modelsList.Count != weaponsSounds.Count) && weapAnim.Count != modelsList.Count)
                throw new Exception("Weapons Loading Error - Arrays of different lengths");

            _weaponsAmount = modelsList.Count;
            _weaponsArray = new List<WeaponData>();

            // Initializing sounds
            for (int i = 0; i < weaponsSounds.Count; i++)
                for (int x = 0; x < weaponsSounds[i].Length; x++)
                {
                    if (weaponsSounds[i][x] != "")
                    {
                        CSoundManager.AddSound("WEP." + weaponsSounds[i][x], content.Load<SoundEffect>(weaponsSounds[i][x]), (bool)weaponsInfo[i][5], (float)weaponsInfo[i][11]);
                        CSoundManager.AddSound("WEP.MULTI." + weaponsSounds[i][x], content.Load<SoundEffect>(weaponsSounds[i][x]), (bool)weaponsInfo[i][5], (float)weaponsInfo[i][11], new AudioListener(), new AudioEmitter());
                    }
                }
            for (int i = 0; i < _weaponsAmount; i++)
            {
                _weaponsArray.Add(new WeaponData(modelsList[(string)weaponsInfo[i][12]], weaponsInfo[i], weaponsSounds[i], weapAnim[i], animVelocity[i], weapTexture[(string)weaponsInfo[i][12]]));
            }

            // We add the switching sounds
            SoundEffect changeWeapSound, changeWeapSound2, changeWeapSound3, pickup;
            changeWeapSound = content.Load<SoundEffect>("Sounds\\Weapons\\CHANGEWEAPON1");
            changeWeapSound2 = content.Load<SoundEffect>("Sounds\\Weapons\\CHANGEWEAPON2");
            changeWeapSound3 = content.Load<SoundEffect>("Sounds\\Weapons\\SWITCH_MACHETE");
            pickup = content.Load<SoundEffect>("Sounds\\Weapons\\PICKUPWEAPON");
            CSoundManager.AddSound("SWITCHWEAPON1", changeWeapSound, false, 0.0f);
            CSoundManager.AddSound("SWITCHWEAPON2", changeWeapSound2, false, 0.0f);
            CSoundManager.AddSound("PICKUPWEAPON", pickup, false, 0.0f);
            CSoundManager.AddSound("SWITCH_MACHETE", changeWeapSound3, false, 0.0f);

            // Initialize the weapon possessed
            _weaponPossessed = new List<WeaponData>();
            _weaponPossessed.Add(_weaponsArray[0]);
        }

        public void ChangeWeapon(int newWeapon)
        {
            _selectedWeapon = newWeapon;
        }

        public void Shot(bool firstShot, bool isCutAnimPlaying, GameTime gameTime)
        {
            if (_weaponPossessed[_selectedWeapon]._wepType != 2)
            {
                if (firstShot)
                    _dryFirePlayed = false;
                if (firstShot && !_weaponPossessed[_selectedWeapon]._isAutomatic)
                    InternFire();
                else if (_weaponPossessed[_selectedWeapon]._isAutomatic)
                {
                    if (gameTime.TotalGameTime.TotalMilliseconds - _lastShotMs >= _weaponPossessed[_selectedWeapon]._shotPerSeconds)
                    {
                        InternFire();
                        _lastShotMs = gameTime.TotalGameTime.TotalMilliseconds;
                    }
                }
            }
            else
            {

                if (isCutAnimPlaying)
                {
                    CSoundManager.PlayInstance("WEP." + _weaponPossessed[_selectedWeapon]._shotSound);
                }
            }
        }

        private void InternFire()
        {
            if (_weaponPossessed[_selectedWeapon]._actualClip > 0)
            {
                _weaponPossessed[_selectedWeapon]._actualClip--;
                CSoundManager.PlaySound("WEP." + _weaponPossessed[_selectedWeapon]._shotSound);
            }
            else
            {
                // DO not play the dry fire if the weapon is a bow
                if (_weaponPossessed[_selectedWeapon]._wepType != 3)
                {
                    if (!_dryFirePlayed)
                    {
                        CSoundManager.PlaySound("WEP." + _weaponPossessed[_selectedWeapon]._dryShotSound);
                        _dryFirePlayed = true;
                    }
                }
            }
            //Console.WriteLine("Weapon : " + _weaponPossessed[_selectedWeapon]._name + " Bullet avaible : " + _weaponPossessed[_selectedWeapon]._bulletsAvailable + " \n ActualClip : " + _weaponPossessed[_selectedWeapon]._actualClip);
        }

        public bool Reloading()
        {
            bool isRealoadingDone = false;
            if (_weaponPossessed[_selectedWeapon]._wepType != 2)
            {
                if (_weaponPossessed[_selectedWeapon]._actualClip < _weaponPossessed[_selectedWeapon]._maxClip)
                {
                    // If he has bullets available to fill a mag
                    if (_weaponPossessed[_selectedWeapon]._bulletsAvailable >=
                            (_weaponPossessed[_selectedWeapon]._maxClip - _weaponPossessed[_selectedWeapon]._actualClip))
                    {
                        _weaponPossessed[_selectedWeapon]._bulletsAvailable -= (_weaponPossessed[_selectedWeapon]._maxClip - _weaponPossessed[_selectedWeapon]._actualClip);
                        _weaponPossessed[_selectedWeapon]._actualClip = _weaponPossessed[_selectedWeapon]._maxClip;
                        isRealoadingDone = true;
                    }

                    else if (_weaponPossessed[_selectedWeapon]._bulletsAvailable > 0
                        && (_weaponPossessed[_selectedWeapon]._actualClip + _weaponPossessed[_selectedWeapon]._bulletsAvailable) <= _weaponPossessed[_selectedWeapon]._maxClip)
                    {
                        _weaponPossessed[_selectedWeapon]._actualClip += _weaponPossessed[_selectedWeapon]._bulletsAvailable;
                    }
                }

            }

            return isRealoadingDone;
        }

        // Add weapon to weaponPossessed list
        public void GiveWeapon(string weaponName, int bullets)
        {
            bool exist = false;
            int index = 0; // We store the weapon index if the weapon is already possessed
            int i = 0; // Just used as a counter

            foreach (WeaponData weap in _weaponPossessed)
            {
                if (weap._name == weaponName)
                {
                    exist = true;
                    index = i;

                    break;
                }
                i++;
            }

            // If the weapon is not possessed
            if (!exist)
            {
                foreach (WeaponData weap in _weaponsArray)
                {
                    if (weap._name == weaponName)
                    {
                        _weaponPossessed.Add(weap);
                        break;
                    }
                }
                
            }
            else // We give bullets
            {
                _weaponPossessed[index]._bulletsAvailable += bullets;
            }
        }

    }
}
