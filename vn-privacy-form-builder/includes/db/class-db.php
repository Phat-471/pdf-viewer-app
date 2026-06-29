<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

class VN_Privacy_DB {
	
	public static function create_tables() {
		global $wpdb;
		$charset_collate = $wpdb->get_charset_collate();
		
		require_once ABSPATH . 'wp-admin/includes/upgrade.php';
		
		// 1. Table for custom privacy forms configuration
		$table_forms = $wpdb->prefix . 'vn_privacy_forms';
		$sql_forms = "CREATE TABLE $table_forms (
			id mediumint(9) NOT NULL AUTO_INCREMENT,
			title varchar(255) NOT NULL,
			fields text NOT NULL,
			created_at datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
			PRIMARY KEY  (id)
		) $charset_collate;";
		dbDelta( $sql_forms );
		
		// 2. Table for collected entries and compliance consent logs
		$table_entries = $wpdb->prefix . 'vn_privacy_entries';
		$sql_entries = "CREATE TABLE $table_entries (
			id bigint(20) NOT NULL AUTO_INCREMENT,
			form_id mediumint(9) NOT NULL,
			fullname varchar(100) NOT NULL,
			phone varchar(20) NOT NULL,
			message text DEFAULT '' NOT NULL,
			ip_address varchar(45) NOT NULL,
			user_agent varchar(255) NOT NULL,
			consent_time datetime DEFAULT CURRENT_TIMESTAMP NOT NULL,
			PRIMARY KEY  (id)
		) $charset_collate;";
		dbDelta( $sql_entries );
	}
	
	public static function insert_default_forms() {
		global $wpdb;
		$table_forms = $wpdb->prefix . 'vn_privacy_forms';
		
		// Check if forms already exist
		$count = $wpdb->get_var( "SELECT COUNT(*) FROM $table_forms" );
		if ( $count == 0 ) {
			// Demo Form 1: Báo giá
			$wpdb->insert( $table_forms, [
				'title'  => 'Nhận Báo Giá Trọn Gói',
				'fields' => json_encode([
					['type' => 'text', 'name' => 'fullname', 'label' => 'Họ tên của bạn', 'placeholder' => 'Ví dụ: Nguyễn Văn A', 'required' => true],
					['type' => 'tel', 'name' => 'phone', 'label' => 'Số điện thoại', 'placeholder' => 'Ví dụ: 0912345678', 'required' => true],
					['type' => 'textarea', 'name' => 'message', 'label' => 'Yêu cầu chi tiết', 'placeholder' => 'Ví dụ: Cần báo giá bồn cầu, sen cây...', 'required' => false]
				])
			]);
			
			// Demo Form 2: Tư vấn thi công
			$wpdb->insert( $table_forms, [
				'title'  => 'Đăng Ký Tư Vấn Thiết Kế & Thi Công',
				'fields' => json_encode([
					['type' => 'text', 'name' => 'fullname', 'label' => 'Họ và tên', 'placeholder' => 'Ví dụ: Trần Thị B', 'required' => true],
					['type' => 'tel', 'name' => 'phone', 'label' => 'Số điện thoại liên hệ', 'placeholder' => 'Ví dụ: 0987654321', 'required' => true],
					['type' => 'textarea', 'name' => 'message', 'label' => 'Mô tả công trình', 'placeholder' => 'Ví dụ: Phòng tắm biệt thự rộng 5m2...', 'required' => false]
				])
			]);
		}
	}
	
	public static function save_entry( $data ) {
		global $wpdb;
		$table_entries = $wpdb->prefix . 'vn_privacy_entries';
		
		// Secure Database Insertion (Uses Parameterized Queries internally)
		return $wpdb->insert( $table_entries, [
			'form_id'    => intval( $data['form_id'] ),
			'fullname'   => sanitize_text_field( $data['fullname'] ),
			'phone'      => sanitize_text_field( $data['phone'] ),
			'message'    => sanitize_textarea_field( $data['message'] ),
			'ip_address' => sanitize_text_field( $data['ip_address'] ),
			'user_agent' => sanitize_text_field( $data['user_agent'] )
		]);
	}
	
	public static function get_forms() {
		global $wpdb;
		$table_forms = $wpdb->prefix . 'vn_privacy_forms';
		return $wpdb->get_results( "SELECT * FROM $table_forms ORDER BY id DESC" );
	}
	
	public static function get_form( $id ) {
		global $wpdb;
		$table_forms = $wpdb->prefix . 'vn_privacy_forms';
		return $wpdb->get_row( $wpdb->prepare( "SELECT * FROM $table_forms WHERE id = %d", intval( $id ) ) );
	}
	
	public static function get_entries( $form_id = 0, $filter_month = '' ) {
		global $wpdb;
		$table_entries = $wpdb->prefix . 'vn_privacy_entries';
		$table_forms   = $wpdb->prefix . 'vn_privacy_forms';
		
		$where = [];
		$params = [];
		
		if ( $form_id ) {
			$where[] = "e.form_id = %d";
			$params[] = intval( $form_id );
		}
		
		if ( ! empty( $filter_month ) ) {
			// Month format: YYYY-MM
			$where[] = "DATE_FORMAT(e.consent_time, '%Y-%m') = %s";
			$params[] = sanitize_text_field( $filter_month );
		}
		
		$where_clause = '';
		if ( ! empty( $where ) ) {
			$where_clause = "WHERE " . implode( " AND ", $where );
		}
		
		$query = "
			SELECT e.*, f.title as form_title 
			FROM $table_entries e
			LEFT JOIN $table_forms f ON e.form_id = f.id
			$where_clause
			ORDER BY e.id DESC
		";
		
		if ( ! empty( $params ) ) {
			return $wpdb->get_results( $wpdb->prepare( $query, $params ) );
		}
		
		return $wpdb->get_results( $query );
	}
	
	public static function delete_entry( $id ) {
		global $wpdb;
		$table_entries = $wpdb->prefix . 'vn_privacy_entries';
		return $wpdb->delete( $table_entries, [ 'id' => intval( $id ) ], [ '%d' ] );
	}
}
