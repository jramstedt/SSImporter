/*
public class ForceFieldRendererFeature : ScriptableRendererFeature {
  private AsyncOperationHandle<Palette> palette;
  private AsyncOperationHandle<ShadeTableData> shadeTable;
  private ForceFieldPass forceFieldPass;

  public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
    renderer.EnqueuePass(this.forceFieldPass);
  }

  public override void Create() {
    this.palette = Services.Palette;
    this.shadeTable = Services.ShadeTable;
    this.forceFieldPass = new ForceFieldPass();
  }

  private class ForceFieldPass : ScriptableRenderPass {
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {

    }
  }
}
*/