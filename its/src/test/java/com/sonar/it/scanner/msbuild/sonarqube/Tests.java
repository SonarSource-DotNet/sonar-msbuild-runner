package com.sonar.it.scanner.msbuild.sonarqube;

import com.sonar.it.scanner.msbuild.utils.TestUtils;
import com.sonar.orchestrator.Orchestrator;
import com.sonar.orchestrator.container.Edition;
import com.sonar.orchestrator.junit5.OrchestratorExtension;
import com.sonar.orchestrator.locator.FileLocation;
import org.junit.jupiter.api.extension.AfterAllCallback;
import org.junit.jupiter.api.extension.BeforeAllCallback;
import org.junit.jupiter.api.extension.ExtensionContext;

public class Tests implements BeforeAllCallback, AfterAllCallback {

  public static final Orchestrator ORCHESTRATOR = createOrchestrator();

  private volatile int usageCount;

  @Override
  public void beforeAll(ExtensionContext extensionContext) {
    usageCount += 1;
    if (usageCount == 1) {
      ORCHESTRATOR.start();
    }
  }

  @Override
  public void afterAll(ExtensionContext extensionContext) throws Exception {
    usageCount -= 1;
    if (usageCount == 0) {
      ORCHESTRATOR.stop();
    }
  }

  private static Orchestrator createOrchestrator() {
    var version = TestUtils.replaceLtsVersion(System.getProperty("sonar.runtimeVersion", "DEV"));
    var orchestrator = OrchestratorExtension.builderEnv()
      .useDefaultAdminCredentialsForBuilds(true)
      .setSonarVersion(version)
      .setEdition(Edition.DEVELOPER)
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.cpp", "sonar-cfamily-plugin", System.getProperty("sonar.cfamilyplugin.version", "LATEST_RELEASE")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.css", "sonar-css-plugin", System.getProperty("sonar.css.version", "LATEST_RELEASE")))
      .addPlugin(FileLocation.of(TestUtils.getCustomRoslynPlugin().toFile()))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-csharp-plugin", System.getProperty("sonar.csharpplugin.version", "DEV")))
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.dotnet", "sonar-vbnet-plugin", System.getProperty("sonar.vbnetplugin.version", "DEV")))
      // The following plugin versions are hardcoded because `DEV` is not compatible with SQ < 8.9, to be fixed with this issue: https://github.com/SonarSource/sonar-scanner-msbuild/issues/1486
      .addPlugin(TestUtils.getMavenLocation("org.sonarsource.javascript", "sonar-javascript-plugin", System.getProperty("sonar.javascriptplugin.version", "7.4.4.15624")))
      .addPlugin(TestUtils.getMavenLocation("com.sonarsource.plsql", "sonar-plsql-plugin", System.getProperty("sonar.plsqlplugin.version", "3.6.1.3873")))
      .activateLicense();

    // The number of results depends on the XML plugin version. Since not all plugin versions are compatible with
    // all SonarQube versions, we will run tests with XML plugin on the latest SQ version.
    if ("LATEST_RELEASE".equals(version)) {
      orchestrator.addPlugin(TestUtils.getMavenLocation("org.sonarsource.xml", "sonar-xml-plugin", System.getProperty("sonar.xmlplugin.version", "LATEST_RELEASE")));
    }
    return orchestrator.build();
  }
}
