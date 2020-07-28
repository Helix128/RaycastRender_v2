using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
public class RaycastRenderer : MonoBehaviour
{
    public Vector2 resolution = new Vector2(400,225);
    public float traceBias = 0.02f;

    RenderTexture tex;
    Texture2D target;
    public RawImage img;
    Ray ray;
    public Light Sun;
    [ColorUsage(true,true)]
    public Color SkyColor = Color.blue;
    public bool Phong;
    public bool Shadows;
    public bool SelfShadows;
    public bool Occlusion;
    public bool Reflections;
    public bool Downsample; 
    public bool async = true;
     
    float time;
    
   
    private void Start()
    {
        time = Time.realtimeSinceStartup;
        Render();
    }
    [BurstCompile]
    void Render()
    {
        target = new Texture2D((int)resolution.x, (int)resolution.y);

        img.texture = target;
        img.GetComponent<AspectRatioFitter>().aspectRatio = resolution.x / resolution.y;
        target.filterMode = FilterMode.Bilinear;
        tex = new RenderTexture((int)resolution.x, (int)resolution.y, 0);
        Camera.main.targetTexture = tex;

        if (!async)
        {
            if (!Downsample)
            {
                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {


                        ray = Camera.main.ScreenPointToRay(Vector3.right * x + Vector3.up * y);

                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit, 1000))
                        {
                            Color PhongDiffuse = Color.white;
                            Vector3 smoothNormal = SmoothedNormal(hit);
                            if (Phong)
                            {
                               
                            
                                PhongDiffuse = SingleColor(Vector3.Dot(smoothNormal, -Sun.transform.forward));
                            }
                           

                            target.SetPixel(x, y, RayToColor(hit) * PhongDiffuse);
                      
                            if (Shadows)
                            {
                                RaycastHit shadowHit;
                                if (Physics.Raycast(hit.point + hit.normal * traceBias, -Sun.transform.forward, out shadowHit, 1000))
                                {
                                    target.SetPixel(x, y, target.GetPixel(x, y) * RenderSettings.ambientLight);
                                 


                                }
                            }
                            if (Reflections)
                            {
                                RaycastHit refHit;
                                if (Physics.Raycast(hit.point + smoothNormal * traceBias, Vector3.Reflect(ray.direction, smoothNormal), out refHit, 1000))
                                {
                                    target.SetPixel(x, y, Alpha1((target.GetPixel(x, y) + (RayToColor(refHit))) / 2));
                              

                                }


                                DrawRay(ray, target.GetPixel(x, y));
                            }
                        }

                        else
                        {
                            target.SetPixel(x, y, Color.clear);
                     
                        }
                        target.Apply();

                    }
                }
            }
            else
            {

                for (int x = 0; x < resolution.x; x += 2)
                {
                    for (int y = 0; y < resolution.y; y += 2)
                    {


                        ray = Camera.main.ScreenPointToRay(Vector3.right * x + Vector3.up * y);

                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit))
                        {

                            Color PhongDiffuse = Color.white;
                            Vector3 smoothNormal = SmoothedNormal(hit);
                            if (Phong)
                            {
                               

                                PhongDiffuse = SingleColor(Vector3.Dot(smoothNormal, -Sun.transform.forward));
                            }
                            target.SetPixel(x, y, RayToColor(hit) * PhongDiffuse);
                        ;
                            if (Shadows)
                            {
                                RaycastHit shadowHit;
                                if (Physics.Raycast(hit.point + hit.normal * traceBias, -Sun.transform.forward, out shadowHit))
                                {
                                    target.SetPixel(x, y, target.GetPixel(x, y) * RenderSettings.ambientLight);
                                   


                                }
                            }
                            if (Reflections)
                            {
                                RaycastHit refHit;
                                if (Physics.Raycast(hit.point + smoothNormal * traceBias, Vector3.Reflect(ray.direction, smoothNormal), out refHit))
                                {
                                    target.SetPixel(x, y, Alpha1((target.GetPixel(x, y) + (RayToColor(refHit) / (refHit.distance))) / 2));
                                   

                                }

                                DrawRay(ray, target.GetPixel(x, y));
                            }

                        }
                        else
                        {

                            target.SetPixel(x, y, Color.clear);


                        }

                        target.SetPixel(x, y + 1, Color.Lerp(target.GetPixel(x, y + 2), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x, y - 1, Color.Lerp(target.GetPixel(x, y - 2), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x - 1, y, Color.Lerp(target.GetPixel(x - 2, y), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x + 1, y, Color.Lerp(target.GetPixel(x + 2, y), target.GetPixel(x, y), 0.15f));

                        target.SetPixel(x - 1, y + 1, Color.Lerp(target.GetPixel(x - 2, y + 2), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x - 1, y - 1, Color.Lerp(target.GetPixel(x - 2, y - 2), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x + 1, y - 1, Color.Lerp(target.GetPixel(x + 2, y - 2), target.GetPixel(x, y), 0.15f));
                        target.SetPixel(x + 1, y + 1, Color.Lerp(target.GetPixel(x + 2, y + 2), target.GetPixel(x, y), 0.15f));


                        target.Apply();

                    }
                }
            }
        }
        else
        {
            //Diffuse Pass
            var results = new NativeArray<RaycastHit>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);

            var commands = new NativeArray<RaycastCommand>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);
       
            for (int x = 0; x < resolution.x; x++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    ray = Camera.main.ScreenPointToRay(Vector3.right * x + Vector3.up * y);



                    commands[Mathf.RoundToInt((x * 1) + (y * resolution.x))] = new RaycastCommand(ray.origin,ray.direction, 1000);
                }
            }

            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));
            RaycastCommand[] mainRays = commands.ToArray();

            handle.Complete();


            RaycastHit[] batchedHit = results.ToArray();
            RaycastHit[] mainHits = batchedHit;

            for (int x = 0; x < resolution.x; x++)
            {
                for (int y = 0; y < resolution.y; y++)
                { if (batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider != null)
                    {
                        Vector3 smoothNormal = SmoothedNormal(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))]);
                        Color PhongDiffuse = Color.white;
                        if (Phong)
                        {

                            PhongDiffuse = SingleColor(Mathf.Clamp(Vector3.Dot(smoothNormal, -Sun.transform.forward), 0.1f, 1)) + RenderSettings.ambientLight;
                        }

                        target.SetPixel(x, y, RayToColor(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))]) * PhongDiffuse * Sun.color * Sun.intensity);
                    }
                    else
                    {
                        target.SetPixel(x, y, SkyColor);
                    }
                  
                }
            }
            bool[] unlitPixels = new bool[mainHits.Length];
            target.Apply();
            results.Dispose();
            commands.Dispose();
            //Reflection Pass 
            if (Reflections)
            {
                results = new NativeArray<RaycastHit>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);

                commands = new NativeArray<RaycastCommand>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);
                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {
                        ray = new Ray(mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].point + mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].normal * traceBias, Vector3.Reflect(mainRays[Mathf.RoundToInt((x * 1) + (y * resolution.x))].direction,SmoothedNormal(mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))])));



                        commands[Mathf.RoundToInt((x * 1) + (y * resolution.x))] = new RaycastCommand(ray.origin, ray.direction, 1000);
                    }
                }

                handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));


                handle.Complete();


                batchedHit = results.ToArray();


                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {

                        if (batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider != null)
                        {
                            target.SetPixel(x, y, Color.Lerp(target.GetPixel(x, y) , RayToColor(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))]),HitGlossiness(mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))])));

                        }
                    }
                }
                target.Apply();
                results.Dispose();
                commands.Dispose();
            }
            RaycastHit[] shadowHits = new RaycastHit[1];
            if (Shadows)
            {
                ///Shadow pass
                results = new NativeArray<RaycastHit>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);

                commands = new NativeArray<RaycastCommand>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);
                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {
                        ray = new Ray(mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].point + mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].normal * traceBias, -Sun.transform.forward);



                        commands[Mathf.RoundToInt((x * 1) + (y * resolution.x))] = new RaycastCommand(ray.origin, ray.direction, 1000);
                    }
                }
              
                handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));


                handle.Complete();
                shadowHits = results.ToArray();

                batchedHit = results.ToArray();


                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {
                        if (target.GetPixel(x, y) != SkyColor)
                        {
                            if (batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider != null)
                            {   if (mainHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider != batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider || SelfShadows)
                                {
                                    target.SetPixel(x, y, Color.Lerp(target.GetPixel(x, y), Alpha1(target.GetPixel(x, y) * (RenderSettings.ambientLight)), 1 - HitMetallic(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))])));
                                    unlitPixels[Mathf.RoundToInt((x * 1) + (y * resolution.x))] = true;
                                }
                            }
                        }
                    }
                }
                target.Apply();
                results.Dispose();
                commands.Dispose();
            }
            if (Occlusion)
            {
                ///AO pass
                results = new NativeArray<RaycastHit>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);

                commands = new NativeArray<RaycastCommand>(Mathf.RoundToInt(resolution.x) * Mathf.RoundToInt(resolution.y), Allocator.TempJob);
                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {
                        ray = new Ray(shadowHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))].point +SmoothedNormal(shadowHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))]) * traceBias, SmoothedNormal(shadowHits[Mathf.RoundToInt((x * 1) + (y * resolution.x))]));



                        commands[Mathf.RoundToInt((x * 1) + (y * resolution.x))] = new RaycastCommand(ray.origin, ray.direction, 1000);
                    }
                }

                handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));


                handle.Complete();


                batchedHit = results.ToArray();


                for (int x = 0; x < resolution.x; x++)
                {
                    for (int y = 0; y < resolution.y; y++)
                    {
                        if (unlitPixels[Mathf.RoundToInt((x * 1) + (y * resolution.x))])
                        {
                            if (batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].collider != null)
                            {
                                target.SetPixel(x, y, Color.Lerp(target.GetPixel(x, y), Alpha1(target.GetPixel(x, y)-(Color.white/(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))].distance*55))), 1 - HitMetallic(batchedHit[Mathf.RoundToInt((x * 1) + (y * resolution.x))])));
                        
                            }
                        }
                    }
                }
                target.Apply();
                results.Dispose();
                commands.Dispose();
            }


        }
        Debug.Log("Rendered in " + (Time.realtimeSinceStartup-time) + " seconds");
        time = Time.realtimeSinceStartup;
        Camera.main.targetTexture = null;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Render();
        }
    }
    void DrawRay(Ray ray, Color color)
    {
        //  Debug.DrawRay(ray.origin, ray.direction*1000,color,10);

    }
    public Color RayToColor(RaycastHit hit)
    {
        if (hit.collider == null)
            return Color.clear;


        MeshRenderer rend = hit.collider.GetComponent<MeshRenderer>();
        Texture2D texture2D = rend.sharedMaterial.mainTexture as Texture2D;
        Color pixelColor;
        if (texture2D != null)
        {
            Vector2 pixelUV = hit.textureCoord;
            pixelUV.x *= texture2D.width;
            pixelUV.y *= texture2D.height;
            Vector2 tiling = rend.sharedMaterial.mainTextureScale;
            pixelColor = Color.white;

            if (texture2D.isReadable)
            {
                pixelColor = texture2D.GetPixel((int)pixelUV.x * (int)tiling.x, (int)pixelUV.y * (int)tiling.y) * rend.sharedMaterial.color * Sun.color;
                return pixelColor;
            }
            else
            {
                pixelColor = pixelColor = rend.sharedMaterial.color;
                return pixelColor;
            }
        }
        else
        {

            pixelColor = rend.sharedMaterial.color;
            return pixelColor;
        }

    }
    public float HitGlossiness(RaycastHit hit)
    {
        if (hit.collider == null)
            return 0;


        MeshRenderer rend = hit.collider.GetComponent<MeshRenderer>();
        return rend.sharedMaterial.GetFloat("_Glossiness");

    }
    public float HitMetallic(RaycastHit hit)
    {
        if (hit.collider == null)
            return 0;


        MeshRenderer rend = hit.collider.GetComponent<MeshRenderer>();
        return rend.sharedMaterial.GetFloat("_Metallic");

    }
    public Vector3 SmoothedNormal(RaycastHit aHit)
    {
        var MC = aHit.collider as MeshCollider;
        if (MC == null)
            return aHit.normal;
        var M = MC.sharedMesh;
        var normals = M.normals;
        var indices = M.triangles;
        var N0 = normals[indices[aHit.triangleIndex * 3 + 0]];
        var N1 = normals[indices[aHit.triangleIndex * 3 + 1]];
        var N2 = normals[indices[aHit.triangleIndex * 3 + 2]];
        var B = aHit.barycentricCoordinate;
        var localNormal = (B[0] * N0 + B[1] * N1 + B[2] * N2).normalized;
        return MC.transform.TransformDirection(localNormal);
    }
    public Color SingleColor(float value)
    {
        return new Color(value, value, value, 1);

    }
    public Color Alpha1(Color color)
    {
        return new Color(color.r, color.g, color.b, 1);
    }
    public Color ClampColor(Color color,float min,float max)
    {
        return new Color(Mathf.Clamp(color.r, min, max), Mathf.Clamp(color.g, min, max), Mathf.Clamp(color.b, min, max));

    }
}
