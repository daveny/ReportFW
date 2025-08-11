$(document).ready(function () {
    console.log("Filter form script loaded");

    // When any filter changes, update URL with filter values
    $('.filter-dropdown, .filter-text, .datepicker').on('change', function () {
        updateUrlWithFilters();
    });

    // Override the Apply Filters button click
    $('.filter-panel button[type="submit"]').on('click', function (e) {
        e.preventDefault();
        submitFilterForm();
    });

    // Make the refresh button use the current filter values
    $('#refreshReport').click(function (e) {
        e.preventDefault();
        submitFilterForm();
    });

    // Get URL parameters
    var urlParams = new URLSearchParams(window.location.search);

    // For each URL parameter, update the corresponding filter control
    urlParams.forEach(function (value, key) {
        console.log('Setting filter', key, 'to', value);

        // Update dropdown
        var dropdown = $('select[name="' + key + '"]');
        if (dropdown.length) {
            dropdown.val(value);
            console.log('Updated dropdown', key, 'to', value);
        }

        // Update other input types as needed
        var input = $('input[name="' + key + '"]');
        if (input.length) {
            input.val(value);
        }

        // Update button groups
        var buttons = $('.filter-button-group[data-param-name="' + key + '"] .filter-button');
        if (buttons.length) {
            buttons.removeClass('btn-primary').addClass('btn-secondary');
            buttons.filter('[data-value="' + value + '"]').removeClass('btn-secondary').addClass('btn-primary');
        }
    });

    // Main function to submit the filter form
    function submitFilterForm() {
        console.log("Submitting filter form");

        // Gather all filter values
        var filterValues = {};

        // Get values from all visible filter controls
        $('.filter-dropdown, .filter-text, .datepicker').each(function () {
            var filter = $(this);
            var name = filter.attr('name');
            if (!name) {
                name = filter.data('param-name');
            }
            var value = filter.val();
            if (name) {
                filterValues[name] = value || '';
            }
        });

        // Get values from button groups if any
        $('.filter-button-group').each(function () {
            var activeButton = $(this).find('.btn-primary');
            if (activeButton.length) {
                var name = activeButton.data('param-name');
                var value = activeButton.data('value');
                if (name) {
                    filterValues[name] = value || '';
                }
            }
        });

        console.log("Collected filter values:", filterValues);

        // Create the URL with all filter values
        // Important: Use the current URL's pathname to avoid losing the template name
        var url = window.location.pathname;
        var queryParams = [];

        for (var key in filterValues) {
            if (filterValues[key] !== '') {
                queryParams.push(key + '=' + encodeURIComponent(filterValues[key]));
            }
        }

        // Add query string if we have parameters
        if (queryParams.length > 0) {
            url += '?' + queryParams.join('&');
        }

        console.log('Redirecting to:', url);
        window.location.href = url;
    }

    function updateUrlWithFilters() {
        var filterValues = {};
        $('.filter-dropdown, .filter-text, .datepicker').each(function () {
            var filter = $(this);
            var name = filter.attr('name');
            if (!name) {
                name = filter.data('param-name');
            }
            var value = filter.val();
            if (name && value) {
                filterValues[name] = value;
            }
        });

        // Update URL without reloading
        if (history.pushState) {
            var url = window.location.pathname;
            var queryParams = [];

            for (var key in filterValues) {
                queryParams.push(key + '=' + encodeURIComponent(filterValues[key]));
            }

            if (queryParams.length > 0) {
                url += '?' + queryParams.join('&');
            }

            window.history.pushState({ path: url }, '', url);
            console.log("Updated URL without reload:", url);
        }
    }

    // Apply Filters button handling
    $(document).on('click', '.apply-filters-btn, button[name="Apply Filters"]', function (e) {
        e.preventDefault();

        console.log("Apply Filters button clicked");

        // Get all chart placeholders
        $('.chart-placeholder').each(function () {
            var placeholder = $(this);
            var neededParams = placeholder.attr('data-needs-params');

            // If there are no more needed parameters, load the chart
            if (!neededParams || neededParams === "") {
                console.log("Loading chart:", placeholder.attr('id'));
                loadChartWithFilters(placeholder);
            } else {
                console.log("Chart still needs parameters:", neededParams);
            }
        });

        // After processing all charts, optionally update URL without reloading
        return false; // Prevent form submission that would reload the page
    });

    // Also modify the support for the "Apply Filters" button in the filter panel
    $('.filter-panel button[type="submit"], .filter-panel .apply-filters-btn').on('click', function (e) {
        e.preventDefault();

        console.log("Filter panel Apply button clicked");

        // Get all chart placeholders and load them
        $('.chart-placeholder').each(function () {
            var placeholder = $(this);
            loadChartWithFilters(placeholder);
        });

        return false; // Prevent default form submission
    });
});