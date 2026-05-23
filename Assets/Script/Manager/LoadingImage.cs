using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


namespace Assets.Script.Manager
{
    internal class LoadingImage : MonoBehaviour
    {
        public Sprite[] sprites;
        public float fps = 40f;

        public Image spriteRenderer;
        private float timer;
        private int frame;
       
        private void Update()
        {
            timer += Time.deltaTime;
            if(timer > 1f / fps)
            {
                frame = (frame +1)%sprites.Length;
                spriteRenderer.sprite = sprites[frame];
                timer = 0f;
                    
            }
        }

    }
}
