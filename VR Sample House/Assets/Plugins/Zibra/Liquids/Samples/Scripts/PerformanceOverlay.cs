using UnityEngine;
using System.Collections;
using com.zibra.liquid.Solver;

public class PerformanceOverlay : MonoBehaviour
{

    string FPSLabel = "";
    private int frameCount;
    private float elapsedTime;
    private void Update()
    {
        // FPS calculation
        frameCount++;
        elapsedTime += Time.unscaledDeltaTime;
        if (elapsedTime > 0.5f)
        {
            double frameRate = System.Math.Round(frameCount / elapsedTime);
            frameCount = 0;
            elapsedTime = 0;

            FPSLabel = "FPS: " + frameRate;
        }
    }

    void OnGUI()
    {
        const int START_X = 30;
        const int START_Y = 55;
        const int BOX_WIDTH = 220;
        const int BOX_HEIGHT = 25;
        int y = -1; // Show FPS above all instances
        int x = 0;

        GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT), FPSLabel);

        ZibraLiquid[] components = FindObjectsOfType<ZibraLiquid>();
        foreach (var liquidInstance in components)
        {
            float ResolutionScale = liquidInstance.EnableDownscale ? liquidInstance.DownscaleFactor : 1.0f;
            float PixelCountScale = ResolutionScale * ResolutionScale;
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Instance: {liquidInstance.name}");
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Grid size: {liquidInstance.GridSize}");
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Render resolution: {ResolutionScale * 100.0f}%");
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Render pixel count: {PixelCountScale * 100.0f}%");
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Max particle count: {liquidInstance.MaxNumParticles}");
            GUI.Box(new Rect(START_X + x * BOX_WIDTH, START_Y + y++ * BOX_HEIGHT, BOX_WIDTH, BOX_HEIGHT),
                    $"Current particle count: {liquidInstance.activeParticleNumber}");
            x++;
            y = 0;
        }
    }
}