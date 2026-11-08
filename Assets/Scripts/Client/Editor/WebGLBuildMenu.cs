using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MiniChess.Client.Editor
{
    /// <summary>
    /// 웹(WebGL) 빌드를 Builds/WebGL 에 만든다(GitHub Pages 배포용).
    ///   - GitHub Pages 는 압축 파일에 Content-Encoding 헤더를 붙이지 못하므로 Gzip + 브라우저 압축 해제(decompression fallback)로 빌드한다.
    ///   - 이 설정은 빌드하는 동안만 바꾸고 끝나면 되돌린다(ProjectSettings.asset 을 바꾼 채로 두지 않는다).
    ///   - 씬은 빌드 설정(EditorBuildSettings)에 켜진 것을 그대로 쓴다.
    ///   - 매번 빌드 캐시를 비우고 만든다(느리지만 빠진 데이터 없이 배포한다).
    /// 빌드하면 에디터의 대상 플랫폼이 WebGL 로 바뀐다(되돌리려면 Build Settings 에서 다시 바꾼다).
    /// </summary>
    public static class WebGLBuildMenu
    {
        public const string OutputPath = "Builds/WebGL";

        [MenuItem("MiniChess/Build/WebGL (GitHub Pages)")]
        public static void BuildFromMenu()
        {
            BuildReport report = Build();
            EditorUtility.DisplayDialog("WebGL 빌드", Describe(report), "확인");
        }

        public static BuildReport Build()
        {
            WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = true;

                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    // 증분 빌드가 이전(실패한) 빌드의 플레이어 데이터를 재사용해 씬이 빠진 적이 있어, 배포용은 항상 캐시를 비우고 만든다.
                    options = BuildOptions.CleanBuildCache,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"[WebGLBuild] {Describe(report)}");
                return report;
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                AssetDatabase.SaveAssets();
            }
        }

        public static string Describe(BuildReport report)
        {
            BuildSummary summary = report.summary;
            return $"{summary.result} - {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s, errors {summary.totalErrors} → {OutputPath}";
        }
    }
}
