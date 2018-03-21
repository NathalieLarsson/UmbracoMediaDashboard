angular.module("umbraco").controller("Mirror.MediaDashboardController", function ($scope, umbRequestHelper, $log, $http, modelsBuilderResource) {

	$http.get('/umbraco/api/ResizeMedia/PreCheck').success(function(data) {
		console.log(data);
	});

	$http.get('/umbraco/api/ResizeMedia/GetAll').success(function (data) {
		console.log(data);
	});
});