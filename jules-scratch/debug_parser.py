from playwright.sync_api import Page, sync_playwright

def run_minimal_debug_script(page: Page):
    """
    This script navigates to the editor, loads a metric, and extracts
    the raw content of the `originalThml` global variable.
    """
    base_url = "https://localhost:44368"
    editor_url = f"{base_url}/Editor/Index"

    print("--- Starting Minimal Debug Script ---")
    print(f"Navigating to {editor_url}...")
    page.goto(editor_url)

    print("Loading metric 'metric01_pivot_color.thtml'...")
    page.get_by_label("Meglévő metrika betöltése").select_option("metric01_pivot_color.thtml")
    page.get_by_role("button", name="Betöltés").click()

    print("Waiting for 2 seconds for fetch and parsing to attempt...")
    page.wait_for_timeout(2000)

    print("Extracting `window.originalThml` content...")

    thtml_content = page.evaluate("() => window.originalThml")

    print("\n--- DEBUGGING OUTPUT: content of `window.originalThml` ---")
    print(thtml_content)
    print("--- END OF DEBUG SCRIPT ---")


def main():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(ignore_https_errors=True)
        page = context.new_page()
        try:
            run_minimal_debug_script(page)
        except Exception as e:
            print(f"An error occurred during the debug script execution: {e}")
            page.screenshot(path="jules-scratch/debug_error.png")
        finally:
            browser.close()

if __name__ == "__main__":
    main()
