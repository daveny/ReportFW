$(document).ready(function () {
    console.log("Filter script loaded and initialized!");

    // Transfer URL parameters to hidden form fields
    var urlParams = new URLSearchParams(window.location.search);
    urlParams.forEach(function (value, key) {
        // Check if a form field with this name already exists
        if ($('form#reportForm input[name="' + key + '"], form#reportForm select[name="' + key + '"]').length === 0) {
            $('<input>').attr({
                type: 'hidden',
                name: key,
                value: value
            }).appendTo('form#reportForm');
        }
    });

    // Update hidden fields when filters change
    $('select.filter-dropdown, input.filter-text, input.datepicker').on('change', function () {
        var name = $(this).attr('name');
        var value = $(this).val();

        // Update or create hidden input
        var hiddenField = $('form#reportForm input[type="hidden"][name="' + name + '"]');
        if (hiddenField.length > 0) {
            hiddenField.val(value);
        } else {
            $('<input>').attr({
                type: 'hidden',
                name: name,
                value: value
            }).appendTo('form#reportForm');
        }
    });

    // Function to inspect what filters are on the page - useful for debugging
    function inspectFilters() {
        console.log("-- Inspecting available filters --");
        console.log("Dropdowns:", $('.filter-dropdown').length);
        console.log("Buttons:", $('.filter-button').length);
        console.log("Text inputs:", $('.filter-text').length);
        console.log("Datepickers:", $('.datepicker').length);
        console.log("Chart placeholders:", $('.chart-placeholder').length);

        // Log filter values
        $('.filter-dropdown').each(function () {
            console.log("Dropdown:", $(this).attr('name'), "=", $(this).val(),
                "param-name:", $(this).data('param-name'));
        });

        // Log chart placeholders
        $('.chart-placeholder').each(function () {
            console.log("Placeholder:", $(this).attr('id'),
                "needs:", $(this).attr('data-needs-params'),
                "token length:", $(this).attr('data-token-b64') ? $(this).attr('data-token-b64').length : 0);
        });
    }

    // Run inspection on page load with slight delay to allow dynamic content to render
    setTimeout(function () {
        inspectFilters();

        // Try to load any charts that don't need parameters
        $('.chart-placeholder').each(function () {
            var placeholder = $(this);
            var neededParamsAttr = placeholder.attr('data-needs-params');

            console.log("Checking placeholder on load:", placeholder.attr('id'), "params:", neededParamsAttr);

            if (!neededParamsAttr || neededParamsAttr === "") {
                console.log("No params needed, loading chart");
                loadChartWithFilters(placeholder);
            }
        });
    }, 500);

    // Handle dropdown filters - use delegation for dynamically added elements
    $(document).on('change', '.filter-dropdown', function () {
        var filter = $(this);
        var paramName = filter.attr('name') || filter.data('param-name');
        var value = filter.val();

        console.log("Dropdown filter changed:", paramName, "=", value);

        if (!paramName) {
            console.warn("Missing parameter name for dropdown:", filter.attr('id'));
            return;
        }

        // Update URL with the filter value
        updateUrlParameter(paramName, value);

        // Check if this filter affects any chart placeholders
        refreshAffectedCharts(paramName, value);
    });

    // Handle button group filters
    $(document).on('click', '.filter-button', function () {
        var button = $(this);
        var paramName = button.attr('data-param-name');
        var value = button.attr('data-value');

        console.log("Button filter clicked:", paramName, "=", value);

        if (!paramName) {
            console.warn("Missing parameter name for button");
            return;
        }

        // Update active state in button group
        button.siblings().removeClass('btn-primary').addClass('btn-secondary');
        button.removeClass('btn-secondary').addClass('btn-primary');

        // Update URL with the filter value
        updateUrlParameter(paramName, value);

        // Check if this filter affects any chart placeholders
        refreshAffectedCharts(paramName, value);
    });

    // Handle text filters with Apply button
    $(document).on('click', '.filter-apply-btn', function () {
        var input = $(this).closest('.input-group').find('.filter-text');
        var paramName = input.attr('name') || input.data('param-name');
        var value = input.val();

        console.log("Text filter applied:", paramName, "=", value);

        if (!paramName) {
            console.warn("Missing parameter name for text input");
            return;
        }

        // Update URL with the filter value
        updateUrlParameter(paramName, value);

        // Check if this filter affects any chart placeholders
        refreshAffectedCharts(paramName, value);
    });

    // Also handle direct text input Enter key
    $(document).on('keypress', '.filter-text', function (e) {
        if (e.which === 13) { // Enter key
            var input = $(this);
            var paramName = input.attr('name') || input.data('param-name');
            var value = input.val();

            console.log("Text filter applied via Enter key:", paramName, "=", value);

            if (!paramName) {
                console.warn("Missing parameter name for text input");
                return;
            }

            // Update URL with the filter value
            updateUrlParameter(paramName, value);

            // Check if this filter affects any chart placeholders
            refreshAffectedCharts(paramName, value);

            // Prevent form submission
            e.preventDefault();
        }
    });

    // Handle calendar filters
    $(document).on('changeDate', '.datepicker', function () {
        var calendar = $(this);
        var paramName = calendar.attr('name') || calendar.data('param-name');
        var value = calendar.val();

        console.log("Calendar filter changed:", paramName, "=", value);

        if (!paramName) {
            console.warn("Missing parameter name for calendar");
            return;
        }

        // Update URL with the filter value
        updateUrlParameter(paramName, value);

        // Check if this filter affects any chart placeholders
        refreshAffectedCharts(paramName, value);
    });

    // Also handle direct date input changes for browsers with native date pickers
    $(document).on('change', '.datepicker', function () {
        var calendar = $(this);
        var paramName = calendar.attr('name') || calendar.data('param-name');
        var value = calendar.val();

        console.log("Calendar input changed directly:", paramName, "=", value);

        if (!paramName) {
            console.warn("Missing parameter name for calendar");
            return;
        }

        // Update URL with the filter value
        updateUrlParameter(paramName, value);

        // Check if this filter affects any chart placeholders
        refreshAffectedCharts(paramName, value);
    });

    // Helper function to update URL with filter parameter
    function updateUrlParameter(key, value) {
        if (!key) {
            console.warn("Cannot update URL: Missing parameter name");
            return;
        }

        var url = window.location.href;
        var re = new RegExp('([?&])' + key + '=.*?(&|$)', 'i');
        var separator = url.indexOf('?') !== -1 ? '&' : '?';

        if (url.match(re)) {
            url = url.replace(re, '$1' + key + '=' + value + '$2');
        } else {
            url = url + separator + key + '=' + value;
        }

        // Update the URL without reloading the page
        window.history.pushState({ path: url }, '', url);
        console.log("URL updated:", url);
    }

    // Helper function to check which charts are affected by a specific filter
    function refreshAffectedCharts(paramName, value) {
        console.log("Checking for charts affected by:", paramName);

        var paramUpdated = false;

        // First check for charts that explicitly list this param as dependency
        $('.chart-placeholder').each(function () {
            var placeholder = $(this);
            var placeholder_id = placeholder.attr('id') || "unknown";

            // Get the needed parameters safely
            var neededParamsAttr = placeholder.attr('data-needs-params');
            if (!neededParamsAttr) {
                return; // Skip if no params attribute
            }

            var neededParams = neededParamsAttr.split(',');

            // If this placeholder needs the parameter that changed
            if (neededParams.includes(paramName)) {
                console.log("Placeholder", placeholder_id, "needs parameter:", paramName);
                paramUpdated = true;

                // Remove this parameter from the needed list
                neededParams = neededParams.filter(function (p) {
                    return p !== paramName;
                });

                // Update the placeholder's data attribute
                placeholder.attr('data-needs-params', neededParams.join(','));

                // Note: We're not automatically loading the chart anymore
                // Just update the message to show what parameters are still needed
                if (neededParams.length === 0) {
                    console.log("All needed params satisfied for", placeholder_id);
                    placeholder.find('.alert').html('All parameters are set. Click "Apply Filters" to update the chart.');
                } else {
                    console.log("Still waiting for params:", neededParams.join(','));
                    placeholder.find('.alert').html('Please select ' +
                        neededParams.map(p => '<strong>' + p + '</strong>').join(', '));
                }
            }
        });

        // We're also removing the auto-refresh for affects relationships
        // If you want to keep track of which charts will be affected, keep this code
        // but remove the loadChartWithFilters calls
        if (!paramUpdated) {
            console.log("Checking 'affects' relationships");

            // Get all filters that might be affected by this change
            $('[data-affects]').each(function () {
                var filter = $(this);
                var affectedCharts = filter.data('affects');

                if (affectedCharts) {
                    // Split by comma if multiple charts are affected
                    var chartIds = affectedCharts.split(',').map(function (id) {
                        return id.trim();
                    });

                    console.log("Filter affects charts:", chartIds.join(', '));

                    // Note: Not automatically refreshing charts anymore
                }
            });
        }
    }

    // Function to refresh a chart by its ID (direct approach)
    function refreshChartById(chartId, paramName, value) {
        console.log("Attempting to refresh chart by ID:", chartId);

        // Only proceed if we have the base form and know the template name
        var form = $('form#reportForm');
        var templateName = form.attr('action').split('/').pop();

        if (!form.length || !templateName) {
            console.warn("Cannot refresh chart: missing form or template name");
            return;
        }

        // Collect all current filter values
        var filterValues = {};

        // Get values from all filter inputs
        $('.filter-dropdown, .filter-text, .datepicker').each(function () {
            var filter = $(this);
            var name = filter.attr('name') || filter.data('param-name');
            var val = filter.val();
            if (name && val) {
                filterValues[name] = val;
            }
        });

        // Add values from button groups
        $('.btn-group').each(function () {
            var activeButton = $(this).find('.btn-primary');
            if (activeButton.length) {
                var name = activeButton.data('param-name');
                var val = activeButton.data('value');
                if (name && val) {
                    filterValues[name] = val;
                }
            }
        });

        // Update or add the changed parameter
        if (paramName && value) {
            filterValues[paramName] = value;
        }

        // Instead of trying to update the individual chart, let's trigger the 
        // "Apply Filters" button to reload the entire report with the new parameters
        var applyBtn = $('.filter-panel button[type="submit"]');
        if (applyBtn.length) {
            console.log("Triggering filter apply button to refresh entire report");
            applyBtn.trigger('click');
        } else {
            // Fallback: reload the page with the new parameters
            var queryString = Object.keys(filterValues)
                .map(function (k) { return k + '=' + encodeURIComponent(filterValues[k]); })
                .join('&');

            var url = window.location.pathname;
            if (queryString) {
                url += '?' + queryString;
            }

            console.log("Reloading page with parameters:", url);
            window.location.href = url;
        }
    }

    // Function to load a chart with all filter values
    function loadChartWithFilters(placeholder) {
        var placeholder_id = placeholder.attr('id') || "unknown";
        console.log("Loading chart for placeholder:", placeholder_id);

        // Show loading indicator
        placeholder.html('<div class="chart-loading"><div class="spinner-border text-primary" role="status"></div> Loading chart...</div>');

        // Get the token from base64-encoded data attribute
        var base64Token = placeholder.attr('data-token-b64');

        if (!base64Token) {
            console.error("Missing base64 token in placeholder:", placeholder_id);
            placeholder.html('<div class="alert alert-danger">Error: Missing chart token</div>');
            return;
        }

        // Decode the base64 token
        var token;
        try {
            token = atob(base64Token);
            console.log("Base64 token decoded successfully (length: " + token.length + ")");

            // Debug the token content
            if (token.length < 100) {
                console.log("Token content:", token);
            } else {
                console.log("Token starts with:", token.substring(0, 100) + "...");
            }
        } catch (e) {
            console.error("Failed to decode base64 token:", e);
            placeholder.html('<div class="alert alert-danger">Error: Invalid chart token encoding</div>');
            return;
        }

        // Check if the token is properly formatted
        //if (!token.includes('query=')) {
        //    console.error("Token doesn't contain a query parameter, cannot proceed");
        //    placeholder.html('<div class="alert alert-danger">Error: Missing required \'query\' parameter</div>');
        //    return;
        //}

        if (!token.includes('query=')) {
            console.warn("Token might be missing query parameter, attempting to proceed anyway");
        }

        // Collect all current filter values
        var filterValues = {};

        // Get values from dropdowns
        $('select.filter-dropdown').each(function () {
            var filter = $(this);
            var paramName = filter.attr('name') || filter.data('param-name');
            if (paramName) {
                filterValues[paramName] = filter.val() || '';
            }
        });

        // Get values from text inputs
        $('input.filter-text').each(function () {
            var filter = $(this);
            var paramName = filter.attr('name') || filter.data('param-name');
            if (paramName) {
                filterValues[paramName] = filter.val() || '';
            }
        });

        // Get values from datepickers
        $('input.datepicker').each(function () {
            var filter = $(this);
            var paramName = filter.attr('name') || filter.data('param-name');
            if (paramName) {
                filterValues[paramName] = filter.val() || '';
            }
        });

        // Get values from button groups
        $('.btn-group').each(function () {
            var activeButton = $(this).find('.btn-primary');
            if (activeButton.length) {
                var paramName = activeButton.attr('data-param-name') || activeButton.data('param-name');
                if (paramName) {
                    filterValues[paramName] = activeButton.attr('data-value') || activeButton.data('value') || '';
                }
            }
        });

        // Also include URL parameters in case some were set manually
        var urlParams = new URLSearchParams(window.location.search);
        urlParams.forEach(function (value, key) {
            if (!filterValues[key]) {
                filterValues[key] = value;
            }
        });

        console.log("Filter values for chart:", filterValues);

        // Prepare form data to properly send complex data
        var formData = new FormData();
        formData.append('token', token);

        // Add each filter value individually to the form data
        for (var key in filterValues) {
            if (filterValues.hasOwnProperty(key)) {
                formData.append(key, filterValues[key]);
            }
        }

        // Send AJAX request to update the chart
        $.ajax({
            url: '/Report/UpdateChart',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                console.log("Chart updated successfully");
                // Replace the placeholder with the chart
                placeholder.replaceWith(response);
            },
            error: function (xhr, status, error) {
                console.error("Error updating chart:", error);
                console.error("Response:", xhr.responseText);
                placeholder.html('<div class="alert alert-danger">' +
                    '<strong>Error loading chart:</strong><br>' +
                    error + '<br><br>' +
                    '<details><summary>Technical Details</summary>' +
                    '<pre>' + xhr.responseText + '</pre></details>' +
                    '</div>');
            }
        });
    }

    // Support for refreshing the entire report
    $('#refreshReport').on('click', function () {
        window.location.reload();
    });

    // Support for exporting to PDF (if implemented)
    $('#exportPdf').on('click', function () {
        alert('PDF export functionality would be implemented here');
    });
});