import re
from playwright.sync_api import Page, expect, sync_playwright

def run_debug_script(page: Page):
    """
    This script navigates to the editor, loads a metric, and extracts
    the content of key JavaScript variables to help with debugging the parsing logic.
    """
    base_url = "https://localhost:44368"
    editor_url = f"{base_url}/Editor/Index"

    print("--- Starting Debug Script ---")
    print(f"Navigating to {editor_url}...")
    page.goto(editor_url)

    print("Loading metric 'metric01_pivot_color.thtml'...")
    page.get_by_label("Meglévő metrika betöltése").select_option("metric01_pivot_color.thtml")
    page.get_by_role("button", name="Betöltés").click()

    print("Waiting for preview to load...")
    preview_frame = page.frame_locator("#previewFrame")
    expect(preview_frame.get_by_role("tab", name="Metric")).to_be_visible(timeout=15000)
    print("Metric loaded. Now extracting debug info...")

    # This function will be executed in the browser's context
    # It mimics the logic from the application's JavaScript to get the variable states.
    debug_info = page.evaluate("""
        () => {
            const result = {};

            // This is a simplified version of the app's splitBlocks function
            const thtml = window.originalThml || '';
            const re = /\\{\\{([\\s\\S]*?)\\}\\}/g;
            let m;
            const originalBlocks = [];
            while ((m = re.exec(thtml)) !== null){
              const inner = m[1];
              const isChart  = /representation\\s*=\\s*['"](barchart|linechart|piechart|table)['"]/i.test(inner);
              originalBlocks.push({ inner, isChart });
            }
            const chartBlockIndex = originalBlocks.findIndex(b => b.isChart);
            const chartInner = (chartBlockIndex >= 0) ? originalBlocks[chartBlockIndex].inner : 'CHART_BLOCK_NOT_FOUND';

            result.chartInner = chartInner;

            const formattingMatch = chartInner.match(/formatting\\s*=\\s*\\{([\\s\\S]*?)\\}/i);
            result.formattingMatch_found = !!formattingMatch;

            if (formattingMatch && formattingMatch[1]) {
                const content = formattingMatch[1];
                result.content = content;

                const rowStylesRegex = /rowStyles\\s*:\\s*(\\[[\\s\\S]*?\\])/;
                const rowStylesMatch = content.match(rowStylesRegex);
                result.rowStylesMatch_found = !!rowStylesMatch;
                result.rowStylesMatch_result = rowStylesMatch ? rowStylesMatch[0] : "NULL";

                // Forcing a match with a known-good string for sanity check
                const testString = 'rowStyles: [ { a:1 } ]';
                result.rowStylesRegex_test = testString.match(rowStylesRegex) ? "OK" : "FAILED";

            } else {
                result.content = "FORMATTING_BLOCK_CONTENT_NOT_FOUND";
                result.rowStylesMatch_found = false;
                result.rowStylesMatch_result = "N/A";
            }

            return result;
        }
    """)

    print("\n--- DEBUGGING OUTPUT ---")
    print("\n[1] chartInner:")
    print("-----------------")
    print(debug_info.get('chartInner'))

    print("\n[2] Formatting block found?")
    print("--------------------------")
    print(debug_info.get('formattingMatch_found'))

    print("\n[3] Content of formatting block:")
    print("--------------------------------")
    print(debug_info.get('content'))

    print("\n[4] rowStyles block found?")
    print("--------------------------")
    print(debug_info.get('rowStylesMatch_found'))

    print("\n[5] Full match for rowStyles regex:")
    print("-----------------------------------")
    print(debug_info.get('rowStylesMatch_result'))

    print("\n[6] Sanity check on test string:")
    print("--------------------------------")
    print(debug_info.get('rowStylesRegex_test'))
    print("\n--- END OF DEBUG SCRIPT ---")


def main():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(ignore_https_errors=True)
        page = context.new_page()
        try:
            run_debug_script(page)
        except Exception as e:
            print(f"An error occurred during the debug script execution: {e}")
            page.screenshot(path="jules-scratch/debug_error.png")
        finally:
            browser.close()

if __name__ == "__main__":
    main()
