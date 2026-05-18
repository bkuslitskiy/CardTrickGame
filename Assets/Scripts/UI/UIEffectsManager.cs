using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIEffectsManager : MonoBehaviour
{
    public static UIEffectsManager Instance { get; private set; }
    private void Awake() { Instance = this; }

    public void EmitSparks(Transform target)
    {
        for (int i = 0; i < 20; i++)
        {
            var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(target, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(10f, 10f);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 0.9f, 0.2f);
            
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(100f, 400f);
            StartCoroutine(AnimateParticle(rt, new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed), 0f, 0.5f));
        }
    }

    public void EmitConfetti(Transform parent)
    {
        for (int i = 0; i < 80; i++)
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-600f, 600f), 600f);
            rt.sizeDelta = new Vector2(15f, 15f);
            go.GetComponent<Image>().color = UnityEngine.Random.ColorHSV(0f, 1f, 1f, 1f, 1f, 1f);
            StartCoroutine(AnimateParticle(rt, new Vector2(UnityEngine.Random.Range(-150f, 150f), UnityEngine.Random.Range(100f, 400f)), -1200f, 4f));
        }
    }

    private IEnumerator AnimateParticle(RectTransform rt, Vector2 vel, float gravity, float duration)
    {
        float time = 0;
        while (time < duration && rt != null)
        {
            time += Time.deltaTime; vel.y += gravity * Time.deltaTime; rt.anchoredPosition += vel * Time.deltaTime;
            if (gravity < 0) rt.localEulerAngles += new Vector3(0, 0, vel.x * Time.deltaTime);
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }
}